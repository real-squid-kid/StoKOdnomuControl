Option Strict On
Imports System.Diagnostics
Imports System.Threading
Imports System.Threading.Tasks

' Арбитраж отделён от USB, чтобы проверить его без приёмника.
' Несколько новых нажатий в одном отчёте дают ничью, а не преимущество младшего номера.
Public Class BuzzerGate
    Private _lastMask As Integer
    Private _waitForRelease As Boolean
    Public Property Armed As Boolean

    Public Sub Arm()
        ' Известная как удерживаемая кнопка должна сначала отпуститься, иначе новый розыгрыш
        ' автоматически выиграл бы пульт, на котором ещё держат кнопку прошлого ответа.
        Armed = True
        _waitForRelease = _lastMask <> 0
    End Sub

    Public Sub Disarm()
        Armed = False
    End Sub

    Public Function Accept(mask As Integer) As Integer
        ' В mask младшие четыре бита соответствуют красным кнопкам пультов 0..3.
        ' Возврат: -1 — нет принятого нажатия, -2 — ничья, 0..3 — победитель.
        Dim pressed = mask And Not _lastMask
        _lastMask = mask
        If Not Armed Then Return -1
        If _waitForRelease Then
            If mask = 0 Then _waitForRelease = False
            Return -1
        End If
        If pressed = 0 Then Return -1
        Armed = False
        ' У числа с несколькими установленными битами выражение x And (x - 1) не равно 0.
        If (pressed And (pressed - 1)) <> 0 Then Return -2
        For pad = 0 To 3
            If (pressed And (1 << pad)) <> 0 Then Return pad
        Next
        Return -1
    End Function
End Class

Public Enum BuzzState
    Disconnected
    Connecting
    Ready
    Armed
    Winner
    Tie
    Faulted
    Disposed
End Enum

' Неизменяемый снимок. Кнопки определяются состоянием, а не словами в Message.
Public NotInheritable Class BuzzStatus
    Public ReadOnly Property State As BuzzState
    Public ReadOnly Property Message As String
    Public ReadOnly Property Warning As String
    Public ReadOnly Property WinningPad As Integer

    Public Sub New(state As BuzzState, Optional message As String = "", Optional winner As Integer = -1, Optional warning As String = "")
        Me.State = state
        Me.Message = message
        WinningPad = winner
        Me.Warning = warning
    End Sub

    Public ReadOnly Property CanConnect As Boolean
        Get
            Return State = BuzzState.Disconnected OrElse State = BuzzState.Faulted
        End Get
    End Property
    Public ReadOnly Property CanArm As Boolean
        Get
            Return State = BuzzState.Ready OrElse State = BuzzState.Winner OrElse State = BuzzState.Tie
        End Get
    End Property
    Public ReadOnly Property CanDisarm As Boolean
        Get
            Return State = BuzzState.Armed
        End Get
    End Property
End Class

Public NotInheritable Class BuzzManager
    Implements IDisposable
    Public Event BuzzerPressed(pad As Integer)
    Public Event StatusChanged As EventHandler
    Private ReadOnly _gate As New Object()
    Private ReadOnly _openReceiver As Func(Of IBuzzReceiver)
    Private _status As New BuzzStatus(BuzzState.Disconnected, "Не подключён")
    Private _input As New BuzzerGate()
    Private _cancel As CancellationTokenSource
    Private _worker As Task = Task.CompletedTask
    Private _disposed As Boolean
    Private _activePad As Integer = -1
    Private _ledDirty As Boolean
    Private _lastLed As Boolean
    Private ReadOnly _blink As New Stopwatch()
    Public Const DefaultLampDelayMilliseconds As Integer = 250
    Public Const MaxLampDelayMilliseconds As Integer = 5000
    Private _lampDelayMilliseconds As Integer = DefaultLampDelayMilliseconds
    Private _winningLampDelayMilliseconds As Integer

    Public Property LampDelayMilliseconds As Integer
        Get
            SyncLock _gate
                Return _lampDelayMilliseconds
            End SyncLock
        End Get
        Set(value As Integer)
            If value < 0 OrElse value > MaxLampDelayMilliseconds Then Throw New ArgumentOutOfRangeException(NameOf(value))
            SyncLock _gate
                _lampDelayMilliseconds = value
            End SyncLock
        End Set
    End Property

    Public Sub New(Optional openReceiver As Func(Of IBuzzReceiver) = Nothing)
        _openReceiver = If(openReceiver, AddressOf HidBuzzReceiver.Open)
    End Sub

    Public ReadOnly Property Status As BuzzStatus
        Get
            SyncLock _gate
                Return _status
            End SyncLock
        End Get
    End Property

    Public Function ConnectAsync() As Task
        Dim ready As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        SyncLock _gate
            If _disposed Then Throw New ObjectDisposedException(NameOf(BuzzManager))
            If Not _status.CanConnect Then Return Task.CompletedTask
            Dim previous = _worker
            Dim cancellation As New CancellationTokenSource()
            _cancel = cancellation
            _status = New BuzzStatus(BuzzState.Connecting, "Поиск приёмника…")
            ' И поиск, и ожидание прежнего чтения происходят вне UI. Старый цикл
            ' полностью освобождает свой HID-дескриптор до открытия следующего.
            _worker = Task.Run(Sub() RunConnection(previous, cancellation, ready))
        End SyncLock
        NotifyStatus()
        Return ready.Task
    End Function

    Public Sub Arm()
        SyncLock _gate
            If Not _status.CanArm Then Throw New InvalidOperationException("Приёмник Buzz! не готов к розыгрышу.")
            _input.Arm()
            _activePad = -1
            _ledDirty = True
            _status = New BuzzStatus(BuzzState.Armed, "Ожидание нажатия", warning:=_status.Warning)
        End SyncLock
        NotifyStatus()
    End Sub

    Public Sub Disarm()
        SyncLock _gate
            _input.Disarm()
            _activePad = -1
            _ledDirty = True
            ' Сброс раунда допустим и без USB. Он не делает отключённый приёмник готовым.
            If Not _status.CanArm AndAlso Not _status.CanDisarm Then Return
            _status = New BuzzStatus(BuzzState.Ready, "Приём заблокирован", warning:=_status.Warning)
        End SyncLock
        NotifyStatus()
    End Sub

    Private Sub RunConnection(previous As Task, cancellation As CancellationTokenSource, ready As TaskCompletionSource(Of Boolean))
        Dim receiver As IBuzzReceiver = Nothing
        Dim terminal = New BuzzStatus(BuzzState.Disconnected, "Не подключён")
        Try
            previous.GetAwaiter().GetResult()
            If cancellation.IsCancellationRequested Then Return
            receiver = _openReceiver()
            If receiver Is Nothing Then Return
            SyncLock _gate
                If cancellation.IsCancellationRequested Then Return
                _input = New BuzzerGate()
                _activePad = -1
                _ledDirty = True
                _status = New BuzzStatus(BuzzState.Ready, "Приём заблокирован")
            End SyncLock
            NotifyStatus()
            ready.TrySetResult(True)
            While Not cancellation.IsCancellationRequested
                Dim mask = receiver.ReadButtons()
                If cancellation.IsCancellationRequested Then Exit While
                Dim winner = -1
                If mask.HasValue Then
                    SyncLock _gate
                        winner = _input.Accept(mask.Value)
                        If winner >= 0 Then
                            _activePad = winner
                            ' Настройку фиксируем для этого победителя: применение настроек
                            ' во время мигания не сдвигает уже начавшуюся последовательность.
                            _winningLampDelayMilliseconds = _lampDelayMilliseconds
                            _blink.Restart()
                            _ledDirty = True
                            _status = New BuzzStatus(BuzzState.Winner, "Первым нажал пульт " & (winner + 1), winner, _status.Warning)
                        ElseIf winner = -2 Then
                            _status = New BuzzStatus(BuzzState.Tie, "Ничья · переигровка", warning:=_status.Warning)
                        End If
                    End SyncLock
                End If
                If winner <> -1 Then NotifyStatus()
                If winner >= 0 Then RaiseEvent BuzzerPressed(winner)
                UpdateLeds(receiver)
            End While
        Catch ex As Exception
            terminal = New BuzzStatus(BuzzState.Faulted, ex.Message)
        Finally
            ' Ни UI, ни повторный Connect не закрывают handle во время ReadButtons.
            If receiver IsNot Nothing Then
                Try
                    receiver.SetLamp(-1, False)
                Catch ex As Exception
                    Debug.WriteLine(ex.Message)
                End Try
                Try
                    receiver.Dispose()
                Catch ex As Exception
                    Debug.WriteLine(ex.Message)
                End Try
            End If
            SyncLock _gate
                _input.Disarm()
                _activePad = -1
                If Not _disposed Then _status = terminal
                If _cancel Is cancellation Then _cancel = Nothing
                cancellation.Dispose()
            End SyncLock
            NotifyStatus()
            ready.TrySetResult(False)
        End Try
    End Sub

    Private Sub UpdateLeds(receiver As IBuzzReceiver)
        Dim pad As Integer, lit As Boolean
        SyncLock _gate
            pad = _activePad
            lit = pad >= 0 AndAlso LampIsLit(_blink.ElapsedMilliseconds, _winningLampDelayMilliseconds)
            If Not _ledDirty AndAlso lit = _lastLed Then Return
            _lastLed = lit
            _ledDirty = False
        End SyncLock
        Dim success As Boolean
        Try
            success = receiver.SetLamp(pad, lit)
        Catch ex As Exception
            Debug.WriteLine(ex.Message)
        End Try
        Dim warning = If(success, "", "Не удалось обновить лампы")
        SyncLock _gate
            If warning = _status.Warning OrElse _disposed Then Return
            _status = New BuzzStatus(_status.State, _status.Message, _status.WinningPad, warning)
        End SyncLock
        ' Отказ ламп не блокирует рабочее чтение кнопок и не изображает потерю USB.
        NotifyStatus()
    End Sub

    ' Не задерживаем чтение USB и определение победителя. До срока лампа погашена;
    ' все 15 секунд мигания отсчитываются от конца задержки, а не от нажатия.
    Friend Shared Function LampIsLit(elapsedMilliseconds As Long, delayMilliseconds As Integer) As Boolean
        Dim blinkingFor = elapsedMilliseconds - delayMilliseconds
        Return blinkingFor >= 0 AndAlso blinkingFor < 15000 AndAlso (blinkingFor \ 125) Mod 2 = 0
    End Function

    Private Sub NotifyStatus()
        ' Событие не содержит устаревающий снимок: UI читает актуальный Status,
        ' даже если несколько сообщений успели накопиться в очереди BeginInvoke.
        RaiseEvent StatusChanged(Me, EventArgs.Empty)
    End Sub

    Public Function ShutdownAsync() As Task
        Dispose()
        SyncLock _gate
            Return _worker
        End SyncLock
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        SyncLock _gate
            If _disposed Then Return
            _disposed = True
            _input.Disarm()
            If _cancel IsNot Nothing Then _cancel.Cancel()
            _status = New BuzzStatus(BuzzState.Disposed)
        End SyncLock
        ' Освобождение выполняет worker; закрытие формы может дождаться ShutdownAsync,
        ' продолжая обрабатывать сообщения Windows вместо синхронного Wait.
        NotifyStatus()
    End Sub
End Class

