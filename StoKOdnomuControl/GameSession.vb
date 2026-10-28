Option Strict On
Imports System.IO
Imports System.Text
Imports System.Security.Cryptography
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
    Private _restoredFromBackup As Boolean
    Private _initialized As Boolean
    Private Shared ReadOnly Serializer As New XmlSerializer(GetType(SavedMatch))

    Public Sub New(directory As String)
        _directory = Path.GetFullPath(directory)
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
            Return SnapshotExists(Path.Combine(_directory, "game.xml"))
        End Get
    End Property

    Public ReadOnly Property RequiresStartupChoice As Boolean
        Get
            Return Not _initialized AndAlso (HasSavedGame OrElse HasSavedBackup)
        End Get
    End Property

    Private Function SnapshotExists(path As String) As Boolean
        Try
            File.GetAttributes(path)
            Return True
        Catch ex As FileNotFoundException
            Return False
        Catch ex As DirectoryNotFoundException
            Return False
        Catch ex As Exception When TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is IOException
            ' Невозможность проверить файл не даёт разрешения перезаписать его.
            Return True
        End Try
    End Function

    Private Sub EnsureInitialized()
        If RequiresStartupChoice Then Throw New InvalidOperationException("Найден сохранённый матч. Сначала восстановите его или явно начните новый матч.")
    End Sub

    Public Sub Execute(description As String, command As Action(Of MatchState))
        EnsureInitialized()
        Dim candidate = State
        command(candidate)
        GameRules.Validate(candidate)
        ' История пополняется только после успеха команды и проверки всех ограничений.
        _saved.History.Add(New UndoEntry With {.Description = description, .State = _saved.Current})
        If _saved.History.Count > 100 Then _saved.History.RemoveAt(0)
        _saved.Current = candidate
        _initialized = True
        Dim storageMessage = Persist(description)
        ' Ошибка диска не откатывает действие на экране. Сообщаем о ней ПОСЛЕ рендера,
        ' чтобы обновление статуса пульта не затёрло предупреждение об автосохранении.
        RaiseEvent Changed(Me, EventArgs.Empty)
        If storageMessage IsNot Nothing Then RaiseEvent StorageError(storageMessage)
    End Sub

    Public Sub Undo()
        EnsureInitialized()
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

    Public ReadOnly Property HasSavedBackup As Boolean
        Get
            Return SnapshotExists(Path.Combine(_directory, "game.xml.bak"))
        End Get
    End Property

    Public Sub Restore(Optional useBackup As Boolean = False)
        ' Проверка структуры и всех снимков завершается до публикации.
        Dim loaded = SavedMatchReader.Read(Path.Combine(_directory, If(useBackup, "game.xml.bak", "game.xml")))
        _saved = loaded
        _initialized = True
        _restoredFromBackup = useBackup
        RaiseEvent Changed(Me, EventArgs.Empty)
    End Sub

    Public Sub StartNewMatch()
        ' Архив не участвует во вращении .bak. Даже новый матч после перезапуска
        ' не должен уничтожать единственную копию старого состояния и его истории.
        ArchivePreviousMatch()
        _initialized = True
        _restoredFromBackup = False
        Execute("Новый матч", Sub(state)
                                   state.Main = New MainGameState()
                                   state.Big = New BigGameState()
                               End Sub)
    End Sub

    Private Sub ArchivePreviousMatch()
        Dim sources = {Path.Combine(_directory, "game.xml"), Path.Combine(_directory, "game.xml.bak")}.Where(Function(path) SnapshotExists(path)).ToArray()
        If sources.Length = 0 Then Return
        Dim archive = Path.Combine(_directory, "archive", "match-" & DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") & "-" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(archive)
        For Each source In sources
            Dim target = Path.Combine(archive, Path.GetFileName(source))
            ' Пока идёт копирование, запрещаем запись/замену источника. Проверяем
            ' байты до разрешения нового матча; ошибка оставляет сессию прежней.
            Using input As New FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read),
                  output As New FileStream(target, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)
                input.CopyTo(output)
                output.Flush(True)
                input.Position = 0
                output.Position = 0
                Using hash = SHA256.Create()
                    Dim expected = hash.ComputeHash(input)
                    If Not expected.SequenceEqual(hash.ComputeHash(output)) Then Throw New IOException("Не удалось проверить архив прежнего матча.")
                End Using
            End Using
        Next
        ' Неполный архив после ошибки не получает отметку завершения.
        AtomicFile.Write(Path.Combine(archive, "COMPLETE.txt"), Encoding.UTF8.GetBytes("Матч сохранён перед явным началом нового. Время каталога указано в UTC."))
    End Sub

    Private Function Persist(description As String) As String
        Try
            Directory.CreateDirectory(_directory)
            Dim target = Path.Combine(_directory, "game.xml")
            ' После восстановления .bak не заменяем хороший резерв повреждённым
            ' основным XML. Первый новый снимок архивирует его отдельно.
            Dim previous = If(_restoredFromBackup, AtomicFile.RejectedPath(target), target & ".bak")
            AtomicFile.Write(target, Sub(stream) Serializer.Serialize(stream, _saved), backupPath:=previous)
            _restoredFromBackup = False
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is InvalidOperationException
            Return "Игра изменена, но автосохранение не удалось: " & ex.Message
        End Try

        ' Журнал — вспомогательный файл. Его отказ не означает потерю уже
        ' опубликованного XML и не должен выдавать ложную ошибку автосохранения.
        Try
            Dim logPath = Path.Combine(_directory, "actions.log")
            If File.Exists(logPath) AndAlso New FileInfo(logPath).Length > 2000000 Then
                File.Move(logPath, Path.Combine(_directory, "actions-" & DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") & "-" & Guid.NewGuid().ToString("N") & ".log"))
            End If
            Dim main = _saved.Current.Main
            Dim line = DateTimeOffset.Now.ToString("o") & " | " & description.Replace(ControlChars.Cr, " ").Replace(ControlChars.Lf, " ") & " | " & main.LeftScore & ":" & main.RightScore & " банк=" & main.Bank & " большая=" & _saved.Current.Big.Total
            File.AppendAllText(logPath, line & Environment.NewLine, Encoding.UTF8)
            Return Nothing
        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
            Return "Матч сохранён, но запись журнала не удалась: " & ex.Message
        End Try
    End Function
End Class
