Option Strict On
Imports System.IO
Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Threading.Tasks
Imports HidLibrary

' A tie in one report is never resolved by the index of the controller.
Public Class BuzzerGate
    Private _lastMask As Integer
    Private _waitForRelease As Boolean
    Public Property Armed As Boolean

    Public Sub Arm()
        Armed = True
        _waitForRelease = _lastMask <> 0
    End Sub

    Public Sub Disarm()
        Armed = False
    End Sub

    Public Function Accept(mask As Integer) As Integer
        Dim pressed = mask And Not _lastMask
        _lastMask = mask
        If Not Armed Then Return -1
        If _waitForRelease Then
            If mask = 0 Then _waitForRelease = False
            Return -1
        End If
        If pressed = 0 Then Return -1
        Armed = False
        If (pressed And (pressed - 1)) <> 0 Then Return -2
        For pad = 0 To 3
            If (pressed And (1 << pad)) <> 0 Then Return pad
        Next
        Return -1
    End Function
End Class

Public NotInheritable Class BuzzManager
    Implements IDisposable
    Public Event BuzzerPressed(pad As Integer)
    Public Event SimultaneousPress()
    Public Event StatusChanged(message As String)
    Private Const VendorId As Integer = &H54C
    Private Const ProductId As Integer = &H1000
    Private Const HidSuccess As Integer = &H110000
    Private ReadOnly _gate As New Object()
    Private _input As New BuzzerGate()
    Private _device As HidDevice
    Private _worker As Task
    Private _cancel As CancellationTokenSource
    Private _preparsed As IntPtr
    Private _activePad As Integer = -1
    Private _ledDirty As Boolean
    Private _lastLed As Boolean
    Private ReadOnly _blink As New Stopwatch()

    Public Sub Initialize()
        StopWorker()
        Dim device As HidDevice = Nothing
        Try
            Dim devices = HidDevices.Enumerate(VendorId, ProductId).ToArray()
            If devices.Length = 0 Then
                RaiseEvent StatusChanged("Buzz!: приёмник не найден. Подключите USB и нажмите «Подключить Buzz!».")
                Return
            End If
            If devices.Length > 1 Then Throw New InvalidOperationException("Найдено несколько приёмников Buzz!. Оставьте подключённым один.")
            device = devices(0)
            device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.ShareRead Or ShareMode.ShareWrite)
            If Not device.IsOpen Then Throw New InvalidOperationException("Не удалось открыть приёмник.")
            If Not HidD_GetPreparsedData(device.ReadHandle, _preparsed) Then Throw New InvalidOperationException("Не удалось прочитать описание кнопок приёмника.")
            _device = device
            _input = New BuzzerGate()
            _activePad = -1
            _ledDirty = True
            _cancel = New CancellationTokenSource()
            Dim token = _cancel.Token
            _worker = Task.Run(Sub() ReadLoop(device, token))
            RaiseEvent StatusChanged("Buzz!: подключён, кнопки заблокированы. Нажмите «Разрешить Buzz!» перед розыгрышем.")
        Catch ex As Exception
            If _preparsed <> IntPtr.Zero Then
                HidD_FreePreparsedData(_preparsed)
                _preparsed = IntPtr.Zero
            End If
            If device IsNot Nothing Then device.Dispose()
            _device = Nothing
            RaiseEvent StatusChanged("Buzz!: " & ex.Message)
        End Try
    End Sub

    Public Sub Arm()
        SyncLock _gate
            If _device Is Nothing Then Throw New InvalidOperationException("Сначала подключите приёмник Buzz!.")
            _input.Arm()
            _activePad = -1
            _ledDirty = True
        End SyncLock
        RaiseEvent StatusChanged("Buzz!: приём ответов разрешён. Если кнопка удерживается, отпустите её перед новым нажатием.")
    End Sub

    Public Sub Disarm()
        SyncLock _gate
            _input.Disarm()
            _activePad = -1
            _ledDirty = True
        End SyncLock
        RaiseEvent StatusChanged("Buzz!: приём ответов заблокирован.")
    End Sub

    Private Sub ReadLoop(device As HidDevice, token As CancellationToken)
        Try
            While Not token.IsCancellationRequested
                Dim data = device.Read(100)
                If token.IsCancellationRequested Then Exit While
                Select Case data.Status
                    Case HidDeviceData.ReadStatus.Success
                        Dim mask = DecodeReport(data.Data)
                        Dim winner As Integer
                        SyncLock _gate
                            winner = _input.Accept(mask)
                            If winner >= 0 Then
                                _activePad = winner
                                _blink.Restart()
                                _ledDirty = True
                            End If
                        End SyncLock
                        If winner >= 0 Then RaiseEvent BuzzerPressed(winner)
                        If winner = -2 Then RaiseEvent SimultaneousPress()
                    Case HidDeviceData.ReadStatus.WaitTimedOut, HidDeviceData.ReadStatus.NoDataRead
                        ' An idle receiver may not publish another report until a button changes.
                    Case Else
                        Throw New IOException("Приёмник отключён или ошибка чтения. Подключите его заново.")
                End Select
                UpdateLeds(device)
            End While
        Catch ex As Exception
            RaiseEvent StatusChanged("Buzz!: " & ex.Message)
        Finally
            SyncLock _gate
                _input.Disarm()
                _activePad = -1
                _device = Nothing
            End SyncLock
            Try
                WriteLeds(device, -1, False)
            Catch ex As Exception
                Debug.WriteLine(ex.Message)
            End Try
            If _preparsed <> IntPtr.Zero Then
                HidD_FreePreparsedData(_preparsed)
                _preparsed = IntPtr.Zero
            End If
            device.Dispose()
        End Try
    End Sub

    Private Function DecodeReport(report As Byte()) As Integer
        Dim usages(31) As UShort
        Dim count As UInteger = CUInt(usages.Length)
        ' HIDP_Input=0; Usage Page 9=buttons. Parse the receiver's descriptor rather
        ' than assuming byte offsets or using the first unrelated WinMM joystick.
        Dim result = HidP_GetUsages(0, 9US, 0US, usages, count, _preparsed, report, CUInt(report.Length))
        If result <> HidSuccess Then Throw New InvalidDataException("Не удалось разобрать HID-отчёт кнопок (" & result.ToString("X8") & ").")
        Dim mask As Integer
        For i = 0 To CInt(count) - 1
            Dim usage = CInt(usages(i))
            If usage >= 1 AndAlso usage <= 16 AndAlso (usage - 1) Mod 5 = 0 Then mask = mask Or (1 << ((usage - 1) \ 5))
        Next
        Return mask
    End Function

    Private Sub UpdateLeds(device As HidDevice)
        Dim pad As Integer
        Dim light As Boolean
        SyncLock _gate
            pad = _activePad
            light = pad >= 0 AndAlso _blink.Elapsed.TotalSeconds < 15 AndAlso (CLng(_blink.ElapsedMilliseconds) \ 125) Mod 2 = 0
            If Not _ledDirty AndAlso light = _lastLed Then Return
            _lastLed = light
            _ledDirty = False
        End SyncLock
        If Not WriteLeds(device, pad, light) Then RaiseEvent StatusChanged("Buzz!: кнопки читаются, но не удалось обновить лампы.")
    End Sub

    Private Shared Function WriteLeds(device As HidDevice, pad As Integer, light As Boolean) As Boolean
        Dim length = Math.Max(8, CInt(device.Capabilities.OutputReportByteLength))
        Dim command(length - 1) As Byte
        If pad >= 0 AndAlso pad <= 3 AndAlso light Then command(2 + pad) = &HFF
        Return device.Write(command, 100)
    End Function

    Private Sub StopWorker()
        If _cancel Is Nothing Then Return
        _cancel.Cancel()
        If _worker IsNot Nothing AndAlso Not _worker.Wait(1500) Then Throw New InvalidOperationException("Buzz!: чтение ещё завершается. Повторите подключение через несколько секунд.")
        _cancel.Dispose()
        _cancel = Nothing
        _worker = Nothing
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        StopWorker()
    End Sub

    <DllImport("hid.dll", SetLastError:=True)>
    Private Shared Function HidD_GetPreparsedData(handle As IntPtr, ByRef data As IntPtr) As <MarshalAs(UnmanagedType.U1)> Boolean
    End Function

    <DllImport("hid.dll")>
    Private Shared Function HidD_FreePreparsedData(data As IntPtr) As <MarshalAs(UnmanagedType.U1)> Boolean
    End Function

    <DllImport("hid.dll")>
    Private Shared Function HidP_GetUsages(reportType As Integer, usagePage As UShort, linkCollection As UShort, <Out> usages As UShort(), ByRef count As UInteger, preparsed As IntPtr, report As Byte(), reportLength As UInteger) As Integer
    End Function
End Class
