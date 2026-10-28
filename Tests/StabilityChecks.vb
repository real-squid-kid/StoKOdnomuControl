Option Strict On
Imports Microsoft.VisualBasic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Windows.Forms
Imports System.Xml.Serialization
Imports StoKOdnomuControl

' Испытания транзакций и длительного использования отделены от обычных регрессий.
' Любая запись, блокировка и остановка процесса касается только выделенного каталога.
Public Module StabilityChecks
    Public ReadOnly BankNames As String() = {"base 2п24.txt", "base.txt", "base 2п26.txt"}
    Private _check As Action(Of Boolean, String)

    Public Sub Run(directory As String, check As Action(Of Boolean, String))
        _check = check
        IO.Directory.CreateDirectory(directory)
        CheckCopies()
        CheckAtomicWrites(Path.Combine(directory, "atomic"))
        CheckRecovery(Path.Combine(directory, "recovery"))
        CheckBanksAndMatch(Path.Combine(directory, "matches"))
        CheckProfileReuse(Path.Combine(directory, "profiles"))
        Console.WriteLine("Storage and soak results: " & directory)
    End Sub

    Private Function XmlState(state As MatchState) As String
        Using writer As New StringWriter(Globalization.CultureInfo.InvariantCulture)
            NewSerializer().Serialize(writer, state)
            Return writer.ToString()
        End Using
    End Function

    Private Function NewSerializer() As XmlSerializer
        Return New XmlSerializer(GetType(MatchState))
    End Function

    Private Sub CheckCopies()
        Dim state As New MatchState()
        state.Main.LeftScore = 231
        state.Main.RightScore = 432
        state.Main.BankId = 7
        state.Main.LegacySplash = 4
        state.Main.Round = 3
        state.Main.LeftStrikes(1) = True
        state.Main.RightStrikes(2) = True
        GameRules.ShowSplash(state.Main.SplashScreen, "ПЕРЕРЫВ", "ПРОДОЛЖЕНИЕ СЛЕДУЕТ")
        GameRules.ShowMainAnswer(state.Main, 2, "СЕМЬЯ", 30, True)
        GameRules.ShowBigText(state.Big, 5, "ПРАЗДНИК")
        GameRules.ShowBigPoints(state.Big, 5, 45)
        GameRules.SetBigTotal(state.Big, 123)
        state.Big.HideFirstPlayer = True
        state.Big.TargetScore = 333
        Dim before = XmlState(state)
        Dim copy = GameRules.Clone(state)
        _check(XmlState(copy) = before AndAlso copy.Main.LegacySplash = 4, "typed snapshot preserves every serialized field and legacy tag")
        copy.Main.Answers(2).Text = "ДРУГОЙ"
        copy.Main.LeftStrikes(1) = False
        copy.Main.RightStrikes(2) = False
        copy.Main.SplashScreen.Line1 = "НОВАЯ"
        copy.Big.Answers(5).Points = 99
        _check(XmlState(state) = before, "snapshot arrays answers and splash do not share mutable objects")
        Dim timer = Stopwatch.StartNew()
        For i = 1 To 10000
            GameRules.Clone(state)
        Next
        Dim typedTime = timer.Elapsed.TotalMilliseconds / 10000
        timer.Restart()
        For i = 1 To 200
            Using stream As New MemoryStream()
                Dim serializer = NewSerializer()
                serializer.Serialize(stream, state)
                stream.Position = 0
                serializer.Deserialize(stream)
            End Using
        Next
        Console.WriteLine("Snapshot average ms: typed=" & typedTime.ToString("F4", Globalization.CultureInfo.InvariantCulture) & "; XML=" & (timer.Elapsed.TotalMilliseconds / 200).ToString("F4", Globalization.CultureInfo.InvariantCulture))
    End Sub

    Private Sub CheckAtomicWrites(folder As String)
        IO.Directory.CreateDirectory(folder)
        Dim target = Path.Combine(folder, "data.xml")
        Dim original = Encoding.UTF8.GetBytes("<original />")
        AtomicFile.Write(target, Encoding.UTF8.GetBytes("<backup />"))
        AtomicFile.Write(target, original)
        Dim backup = File.ReadAllBytes(target & ".bak")
        Dim failed As Boolean
        Try
            AtomicFile.Write(target, Sub(stream)
                                         stream.WriteByte(60)
                                         Throw New IOException("Simulated disk full", &H80070070)
                                     End Sub)
        Catch ex As IOException
            failed = True
        End Try
        _check(failed AndAlso File.ReadAllBytes(target).SequenceEqual(original) AndAlso File.ReadAllBytes(target & ".bak").SequenceEqual(backup), "partial writer failure preserves primary and backup")
        _check(IO.Directory.GetFiles(folder, "*.tmp").Length = 0, "failed writer removes its temporary file")
        Using locked As New FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read)
            failed = False
            Try
                AtomicFile.Write(target, Encoding.UTF8.GetBytes("new"))
            Catch ex As IOException
                failed = True
            End Try
            _check(failed AndAlso File.ReadAllBytes(target).SequenceEqual(original), "sharing lock cannot publish partial file")
        End Using
        Dim ready = Path.Combine(folder, "writer-ready")
        Dim start As New ProcessStartInfo With {.FileName = Application.ExecutablePath, .Arguments = "--interrupt-write " & Quote(target) & " " & Quote(ready), .UseShellExecute = False, .CreateNoWindow = True, .WorkingDirectory = folder}
        Using child = Process.Start(start)
            Try
                Dim limit = Stopwatch.StartNew()
                While Not File.Exists(ready) AndAlso Not child.HasExited AndAlso limit.ElapsedMilliseconds < 10000
                    Threading.Thread.Sleep(10)
                End While
                _check(File.Exists(ready), "child reached mid-write barrier")
            Finally
                If Not child.HasExited Then child.Kill()
                child.WaitForExit()
            End Try
        End Using
        _check(File.ReadAllBytes(target).SequenceEqual(original) AndAlso File.ReadAllBytes(target & ".bak").SequenceEqual(backup), "process death during temporary write preserves last committed files")
        _check(IO.Directory.GetFiles(folder, "*.tmp").Length = 1, "interrupted process leaves only ignored temporary file")
        AtomicFile.Write(target, Encoding.UTF8.GetBytes("<next />"))
        _check(File.ReadAllText(target) = "<next />" AndAlso File.ReadAllBytes(target & ".bak").SequenceEqual(original), "next save succeeds despite stale temporary file")
    End Sub

    ' Запускается отдельным процессом; родитель завершает его после сигнала готовности.
    Public Sub InterruptedWriter(target As String, ready As String)
        AtomicFile.Write(target, Sub(stream)
                                     Dim fragment = Encoding.UTF8.GetBytes("<unfinished>")
                                     stream.Write(fragment, 0, fragment.Length)
                                     stream.Flush()
                                     File.WriteAllText(ready, "ready")
                                     Threading.Thread.Sleep(Threading.Timeout.Infinite)
                                 End Sub)
    End Sub

    Private Sub CheckRecovery(folder As String)
        Dim game As New GameSession(folder)
        Dim errors As New List(Of String)()
        AddHandler game.StorageError, Sub(message) errors.Add(message)
        game.Execute("score 10", Sub(s) s.Main.LeftScore = 10)
        game.Execute("score 20", Sub(s) s.Main.LeftScore = 20)
        Dim target = Path.Combine(folder, "game.xml")
        File.WriteAllText(target, "<interrupted>")
        Dim before = XmlState(game.State)
        Dim failed As Boolean
        Try
            game.Restore()
        Catch ex As InvalidOperationException
            failed = True
        End Try
        _check(failed AndAlso XmlState(game.State) = before, "bad primary never mutates live match")
        game.Restore(True)
        _check(game.State.Main.LeftScore = 10 AndAlso game.UndoDescription = "score 10", "explicit backup restores state and history")
        Dim goodBackup = File.ReadAllBytes(target & ".bak")
        game.Execute("after recovery", Sub(s) s.Main.RightScore = 30)
        _check(File.ReadAllBytes(target & ".bak").SequenceEqual(goodBackup) AndAlso IO.Directory.GetFiles(folder, "game.xml.rejected-*").Length = 1, "first save after recovery preserves good backup and archives corrupt primary")
        Dim reopened As New GameSession(folder)
        reopened.Restore()
        _check(reopened.State.Main.LeftScore = 10 AndAlso reopened.State.Main.RightScore = 30, "recovered match persists normally")
        reopened.Undo()
        _check(reopened.State.Main.RightScore = 0, "undo works after backup recovery and restart")
        Using locked As New FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read)
            game.Execute("disk unavailable", Sub(s) s.Main.LeftScore = 77)
            _check(game.State.Main.LeftScore = 77 AndAlso errors.Count = 1, "disk failure retains live action and reports storage error")
        End Using
        game.Execute("disk available", Sub(s) s.Main.RightScore = 33)
        reopened.Restore()
        _check(reopened.State.Main.LeftScore = 77 AndAlso reopened.State.Main.RightScore = 33, "subsequent save includes action performed during disk failure")
        Dim log = Path.Combine(folder, "actions.log")
        File.Delete(log)
        IO.Directory.CreateDirectory(log)
        errors.Clear()
        game.Execute("log unavailable", Sub(s) s.Main.RightScore = 34)
        reopened.Restore()
        _check(reopened.State.Main.RightScore = 34 AndAlso errors.Single().StartsWith("Матч сохранён"), "log failure accurately reports successful match save")
    End Sub

    Private Sub CheckBanksAndMatch(folder As String)
        IO.Directory.CreateDirectory(folder)
        Dim source = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../StoKOdnomuControl"))
        Dim game As New GameSession(Path.Combine(folder, "match"))
        Dim failures As New List(Of String)()
        AddHandler game.StorageError, Sub(message) failures.Add(message)
        Dim actions As Integer
        For Each name In BankNames
            Dim original = File.ReadAllBytes(Path.Combine(source, name))
            Dim document = BankDocument.Open(Path.Combine(source, name))
            Dim baseline = BankDocument.Serialize(document.Bank)
            Dim target = Path.Combine(folder, name)
            document.Save(target, document.Bank)
            _check(BankDocument.Serialize(BankDocument.Open(target).Bank) = baseline, "requested year bank roundtrip " & name)
            For match = 1 To 8
                For round = 1 To 4
                    Dim number = round
                    game.Execute("start", Sub(s)
                                              s.Main.Round = number
                                              s.Main.Answers = document.Bank.Rounds(number - 1).Select(Function(a) New AnswerState With {.Text = a.Text, .Points = a.Points}).ToArray()
                                              GameRules.ResetRound(s.Main)
                                          End Sub)
                    Dim expected As Integer = 0
                    For i = 0 To 5
                        Dim index = i
                        Dim answer = document.Bank.Rounds(number - 1)(index)
                        game.Execute("open", Sub(s) GameRules.ShowMainAnswer(s.Main, index, answer.Text, answer.Points, True))
                        expected += answer.Points * RoundRules.Multiplier(number)
                    Next
                    _check(game.State.Main.Bank = expected, "full bank equals unmodified source times multiplier " & name & " round " & round)
                    game.Execute("transfer", Sub(s) GameRules.TransferBank(s.Main, TeamSide.Left))
                    game.Undo()
                    _check(game.State.Main.Bank = expected, "undo transfer restores full bank")
                    game.Execute("splash", Sub(s) GameRules.ShowSplash(s.Main.SplashScreen, "ПЕРЕРЫВ", "ПРОДОЛЖЕНИЕ"))
                    actions += 10
                Next
            Next
            _check(File.ReadAllBytes(Path.Combine(source, name)).SequenceEqual(original), "year bank source unchanged " & name)
        Next
        Dim snapshot = XmlState(game.State)
        Dim restored As New GameSession(Path.Combine(folder, "match"))
        restored.Restore()
        _check(XmlState(restored.State) = snapshot AndAlso failures.Count = 0, "long match and splash survive restart")
        For i = 1 To 100
            restored.Undo()
        Next
        _check(restored.UndoDescription = "", "long match retains bounded last 100 undo entries")
        Console.WriteLine("Long match actions: " & actions)
    End Sub

    Private Sub CheckProfileReuse(folder As String)
        Dim errors As New List(Of String)()
        Dim choices = ProfileCatalog.Discover(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles"), errors)
        _check(errors.Count = 0 AndAlso choices.Count = 2, "real bundled profiles available for persistence soak")
        Dim profiles = choices.Select(Function(choice) AppearanceStorage.Open(choice.ConfigFile)).ToArray()
        Using service As New AppearanceService(folder)
            For i = 0 To 9
                service.Apply(profiles(i Mod profiles.Length))
                _check(service.Current.Profile.Name = profiles(i Mod profiles.Length).Name, "alternating real theme persists " & i)
            Next
            Dim media = IO.Directory.GetFiles(Path.Combine(folder, "media"))
            _check(media.Length = 12, "ten theme switches retain only twelve unique source images")
            Dim originalBytes = profiles.SelectMany(Function(p) p.Styles()).SelectMany(Function(s) s.Assets()).Where(Function(p) p <> "").Distinct(StringComparer.OrdinalIgnoreCase).Sum(Function(p) New FileInfo(p).Length)
            _check(media.Sum(Function(p) New FileInfo(p).Length) = originalBytes, "stored graphics retain source byte size without PNG inflation")
            Dim config = Path.Combine(folder, "config.xml")
            Dim backup = File.ReadAllBytes(config & ".bak")
            File.WriteAllText(config, "<broken>")
            Dim recovered = AppearanceStorage.Open(config & ".bak")
            service.Apply(recovered)
            _check(File.ReadAllBytes(config & ".bak").SequenceEqual(backup) AndAlso IO.Directory.GetFiles(folder, "config.xml.rejected-*").Length = 1, "profile recovery preserves valid backup")
            Using restarted As New AppearanceService(folder)
                _check(restarted.StartupError = "" AndAlso restarted.Current.Profile.Name = recovered.Name, "profile recovery survives restart")
            End Using
            Console.WriteLine("Unique profile media bytes: " & originalBytes)
        End Using
    End Sub

    Private Function Quote(text As String) As String
        Return ChrW(34) & text & ChrW(34)
    End Function
End Module
