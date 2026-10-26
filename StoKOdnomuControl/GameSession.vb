Option Strict On
Imports System.IO
Imports System.Text
Imports System.Xml
Imports System.Xml.Serialization

Public Class UndoEntry
    ' Снимок ДО действия вместе с его операторским описанием.
    Public Property Description As String = ""
    Public Property State As MatchState
End Class

Public Class SavedMatch
    Public Property Current As New MatchState()
    Public Property History As New List(Of UndoEntry)()
End Class

' Общая сессия обоих пультов: команды вызываются из UI-потока.
' Сначала меняется отдельный кандидат; исключение не оставляет игру изменённой наполовину.
Public Class GameSession
    Public Event Changed As EventHandler
    Public Event StorageError As Action(Of String)
    Private _saved As New SavedMatch()
    Private ReadOnly _directory As String

    Public Sub New(directory As String)
        _directory = directory
    End Sub

    Public ReadOnly Property State As MatchState
        Get
            ' Потребители получают копию: изменение пульта или рендера не обходит Execute/Undo.
            Return GameRules.Clone(_saved.Current)
        End Get
    End Property

    Public ReadOnly Property UndoDescription As String
        Get
            Return If(_saved.History.Count = 0, "", _saved.History.Last().Description)
        End Get
    End Property

    Public ReadOnly Property HasSavedGame As Boolean
        Get
            Return File.Exists(Path.Combine(_directory, "game.xml"))
        End Get
    End Property

    Public Sub Execute(description As String, command As Action(Of MatchState))
        Dim candidate = State
        command(candidate)
        GameRules.Validate(candidate)
        ' История пополняется только после успеха команды и проверки всех ограничений.
        _saved.History.Add(New UndoEntry With {.Description = description, .State = _saved.Current})
        If _saved.History.Count > 100 Then _saved.History.RemoveAt(0)
        _saved.Current = candidate
        Dim storageMessage = Persist(description)
        ' Ошибка диска не откатывает действие на экране. Сообщаем о ней ПОСЛЕ рендера,
        ' чтобы обновление статуса пульта не затёрло предупреждение об автосохранении.
        RaiseEvent Changed(Me, EventArgs.Empty)
        If storageMessage IsNot Nothing Then RaiseEvent StorageError(storageMessage)
    End Sub

    Public Sub Undo()
        ' Возвращаем готовый снимок обоих режимов, а не пытаемся выполнить обратную арифметику.
        ' Звуки и работающий таймер в эту историю не входят; рендер не повторяет эффекты.
        If _saved.History.Count = 0 Then Return
        Dim entry = _saved.History.Last()
        _saved.Current = entry.State
        _saved.History.RemoveAt(_saved.History.Count - 1)
        Dim storageMessage = Persist("Отмена: " & entry.Description)
        RaiseEvent Changed(Me, EventArgs.Empty)
        If storageMessage IsNot Nothing Then RaiseEvent StorageError(storageMessage)
    End Sub

    Public Sub Restore()
        Dim loaded As SavedMatch
        Dim settings As New XmlReaderSettings With {.DtdProcessing = DtdProcessing.Prohibit, .XmlResolver = Nothing, .MaxCharactersInDocument = 10000000}
        ' Файл и ВСЯ история проверяются до замены текущей сессии.
        ' DTD/внешние сущности отключены; файл не должен подгружать сторонние данные.
        Using reader = XmlReader.Create(Path.Combine(_directory, "game.xml"), settings)
            loaded = DirectCast(New XmlSerializer(GetType(SavedMatch)).Deserialize(reader), SavedMatch)
        End Using
        If loaded Is Nothing OrElse loaded.History Is Nothing OrElse loaded.History.Count > 100 Then Throw New InvalidDataException("Неверный файл сохранения.")
        GameRules.Validate(loaded.Current)
        For Each entry In loaded.History
            If entry Is Nothing OrElse entry.Description Is Nothing Then Throw New InvalidDataException("Неверная история действий.")
            GameRules.Validate(entry.State)
        Next
        _saved = loaded
        RaiseEvent Changed(Me, EventArgs.Empty)
    End Sub

    Private Function Persist(description As String) As String
        Dim temporary As String = Nothing
        Try
            Directory.CreateDirectory(_directory)
            Dim destination = Path.Combine(_directory, "game.xml")
            ' Уникальное имя не сталкивается с временным файлом прошлой попытки записи.
            temporary = destination & "." & Guid.NewGuid().ToString("N") & ".tmp"
            Using stream = New FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)
                Dim serializer As New XmlSerializer(GetType(SavedMatch))
                serializer.Serialize(stream, _saved)
                ' Просим ОС записать содержимое на диск перед заменой основного файла.
                stream.Flush(True)
            End Using
            If File.Exists(destination) Then
                ' Атомарная замена сохраняет предыдущий целый снимок в .bak.
                ' Пока новый XML не записан, основной файл остаётся прежним.
                ReplaceSnapshot(temporary, destination)
            Else
                File.Move(temporary, destination)
            End If
            Dim logPath = Path.Combine(_directory, "actions.log")
            ' Журнал поясняет действия оператору; восстановление берёт данные из XML, не из лога.
            If File.Exists(logPath) AndAlso New FileInfo(logPath).Length > 2000000 Then File.Move(logPath, Path.Combine(_directory, "actions-" & DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") & ".log"))
            Dim main = _saved.Current.Main
            File.AppendAllText(logPath, DateTimeOffset.Now.ToString("o") & " | " & description.Replace(ControlChars.Cr, " ").Replace(ControlChars.Lf, " ") & " | " & main.LeftScore & ":" & main.RightScore & " банк=" & main.Bank & " большая=" & _saved.Current.Big.Total & Environment.NewLine, Encoding.UTF8)
            Return Nothing
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is InvalidOperationException
            Return "Игра изменена, но автосохранение не удалось: " & ex.Message
        Finally
            ' Убираем только временный файл этой попытки; сохранение и резервную копию не трогаем.
            If temporary IsNot Nothing AndAlso File.Exists(temporary) Then
                Try
                    File.Delete(temporary)
                Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                    Diagnostics.Debug.WriteLine(ex.Message)
                End Try
            End If
        End Try
    End Function

    Private Shared Sub ReplaceSnapshot(temporary As String, destination As String)
        For attempt = 1 To 4
            Try
                File.Replace(temporary, destination, destination & ".bak")
                Return
            Catch ex As IOException When attempt < 4 AndAlso (ex.HResult And &HFFFF) = 32
                ' Код 32 — кратковременный запрет совместного доступа, например от антивируса.
                ' Повторяем только такой сбой: ошибки прав/диска должны дойти до оператора.
                Threading.Thread.Sleep(20)
            End Try
        Next
    End Sub
End Class
