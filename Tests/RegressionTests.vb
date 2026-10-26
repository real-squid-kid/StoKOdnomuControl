Option Strict On
Imports Microsoft.VisualBasic
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports System.Collections.Concurrent
Imports System.Threading
Imports System.Threading.Tasks
Imports StoKOdnomuControl

Module Checks
    ' Консольные регрессии без дополнительного тестового фреймворка/NuGet.
    ' Счётчик ниже — число утверждений, а не независимых пользовательских сценариев.
    Private _assertions As Integer
    Private ReadOnly _storageErrors As New List(Of String)()

    <STAThread>
    Public Sub Main(args As String())
        Try
            ' Снимки/логи тестов отделены от сохранения реального матча оператора.
            Dim temporary = Path.Combine(Path.GetTempPath(), "StoKOdnomu-tests-" & Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(temporary)
            TestParser()
            TestMainGame(temporary)
            TestBigGame(temporary)
            TestSplashes(temporary)
            TestLegacySplashes(temporary)
            TestRetiredBigSplash(temporary)
            TestBuzzer()
            TestBuzzConnection()
            TestPersistence(temporary)
            TestCountdown()
            If args.Contains("--ui") Then TestUi(temporary)
            Check(_storageErrors.Count = 0, "no autosave failures: " & String.Join("; ", _storageErrors))
            Console.WriteLine("PASS: " & _assertions & " assertions")
        Catch ex As Exception
            Console.Error.WriteLine(ex.ToString())
            Environment.ExitCode = 1
        End Try
    End Sub

    Private Sub Check(condition As Boolean, description As String)
        If Not condition Then Throw New Exception("FAIL: " & description)
        _assertions += 1
    End Sub

    Private Sub Reject(action As Action, description As String)
        Dim rejected As Boolean
        Try
            action()
        Catch ex As Exception When TypeOf ex Is InvalidDataException OrElse TypeOf ex Is InvalidOperationException
            rejected = True
        End Try
        Check(rejected, description)
    End Sub

    Private Function Session(directory As String, name As String) As GameSession
        Dim game As New GameSession(Path.Combine(directory, name))
        AddHandler game.StorageError, Sub(message) _storageErrors.Add(message)
        Return game
    End Function

    Private Sub TestParser()
        Dim lines = Enumerable.Range(1, 18).Select(Function(i) "Ответ " & i & "&10").Concat(Enumerable.Range(1, 6).Select(Function(i) "Наоборот " & i)).ToArray()
        Dim valid = String.Join(vbCrLf, lines)
        Dim bank = QuestionBankReader.Parse(valid)
        Check(bank.Rounds.Length = 4 AndAlso bank.Rounds(0)(0).Points = 10, "parse existing TXT format")
        Check(bank.Rounds(3)(5).Points = 240, "reverse round prices")
        Check(QuestionBankReader.Parse(valid.Replace(vbCrLf, vbLf)).Rounds(2).Length = 6, "LF input")
        Reject(Sub() QuestionBankReader.Parse(String.Join(vbCrLf, lines.Take(23))), "reject wrong row count")
        Reject(Sub() QuestionBankReader.Parse(valid.Replace("Ответ 1&10", "broken")), "reject missing separator")
        Reject(Sub() QuestionBankReader.Parse(valid.Replace("Ответ 1&10", "Ответ&text")), "reject nonnumeric price")
        Reject(Sub() QuestionBankReader.Parse(valid.Replace("Ответ 1&10", "Ответ&-1")), "reject negative price")
        Reject(Sub() QuestionBankReader.Parse(valid.Replace("Ответ 1&10", "Ответ&1000")), "reject excessive price")
        Reject(Sub() QuestionBankReader.Parse(valid.Replace("Ответ 1&10", " &10")), "reject empty answer")
        Reject(Sub() QuestionBankReader.Parse(valid.Replace("Ответ 1&10", "Ответ&10&20")), "reject ambiguous separator")
        Dim source = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../StoKOdnomuControl"))
        For Each file In Directory.GetFiles(source, "*.txt")
            Check(QuestionBankReader.ReadFile(file).Rounds.Length = 4, "read shipped bank " & Path.GetFileName(file))
        Next
    End Sub

    Private Sub TestCountdown()
        ' Перезапуск часов проверяется с выключенным звуком. Слышимая синхронизация
        ' фонограммы требует отдельной репетиции на оборудовании.
        AudioService.Instance.Enabled = False
        Using countdown As New Countdown()
            countdown.Start(20)
            Check(countdown.Running AndAlso countdown.Visible AndAlso countdown.SecondsRemaining = 20, "start 20-second countdown")
            countdown.Start(25)
            Check(countdown.Running AndAlso countdown.Visible AndAlso countdown.SecondsRemaining = 25, "restart running countdown at 25 seconds")
            countdown.Start(20)
            Check(countdown.Running AndAlso countdown.SecondsRemaining = 20, "restart again at 20 seconds")
            countdown.Stop()
            Check(Not countdown.Running AndAlso Not countdown.Visible AndAlso countdown.SecondsRemaining = 0, "stop countdown clears runtime state")
        End Using
    End Sub

    Private Sub TestMainGame(directory As String)
        Dim game = Session(directory, "main")
        game.Execute("round 2", Sub(s) s.Main.Round = 2)
        game.Execute("open", Sub(s) GameRules.ShowMainAnswer(s.Main, 0, "Ответ", 10, True))
        Check(game.State.Main.Bank = 20 AndAlso game.State.Main.Answers(0).Points = 10, "multiplier changes bank, not display price")
        game.Execute("hide", Sub(s)
                                 s.Main.Answers(0).TextVisible = False
                                 s.Main.Answers(0).ScoreVisible = False
                             End Sub)
        game.Execute("reopen", Sub(s) GameRules.ShowMainAnswer(s.Main, 0, "Ответ", 10, True))
        Check(game.State.Main.Bank = 20, "hide/reopen cannot double count")
        game.Execute("edit", Sub(s) GameRules.EditMainAnswer(s.Main, 0, "Исправленный", 15))
        Check(game.State.Main.Bank = 30, "editing counted answer adjusts by difference")
        game.Undo()
        Check(game.State.Main.Bank = 20 AndAlso game.State.Main.Answers(0).Points = 10, "undo correction")
        game.Execute("transfer left", Sub(s) GameRules.TransferBank(s.Main, TeamSide.Left))
        Check(game.State.Main.LeftScore = 20 AndAlso game.State.Main.RightScore = 0 AndAlso game.State.Main.Bank = 0, "correct transfer side")
        Reject(Sub() game.Execute("invalid edit", Sub(s) GameRules.EditMainAnswer(s.Main, 0, "Ответ", 20)), "cannot rewrite transferred points")
        Check(game.UndoDescription = "transfer left", "rejected action does not pollute history")
        game.Execute("reopen transferred", Sub(s) GameRules.ShowMainAnswer(s.Main, 0, "Ответ", 10, True))
        Check(game.State.Main.Bank = 0 AndAlso game.State.Main.LeftScore = 20, "transferred answer cannot be awarded again")
        game.Undo() : game.Undo()
        Check(game.State.Main.Bank = 20 AndAlso game.State.Main.LeftScore = 0, "undo transfer restores original bank and contribution")
        game.Execute("reset", Sub(s) GameRules.ResetRound(s.Main))
        Check(game.State.Main.Bank = 0 AndAlso Not game.State.Main.Answers(0).TextVisible, "reset actual round state")
        game.Execute("open after reset", Sub(s) GameRules.ShowMainAnswer(s.Main, 0, "Ответ", 10, True))
        Check(game.State.Main.Bank = 20, "reset does not leave stale bank")
        game.Execute("splash", Sub(s) GameRules.ShowSplash(s.Main.SplashScreen, "ДВОЙНАЯ", "ИГРА"))
        Check(game.State.Main.Answers(0).TextVisible AndAlso game.State.Main.Bank = 20, "splash preserves game")
        game.Execute("return", Sub(s) s.Main.SplashScreen.Visible = False)
        Check(game.State.Main.Answers(0).TextVisible, "return from splash restores answer")
        game.Execute("right score", Sub(s) s.Main.RightScore = 123)
        Check(game.State.Main.LeftScore = 0 AndAlso game.State.Main.RightScore = 123, "independent scores")
        Reject(Sub() game.Execute("bad score", Sub(s) s.Main.Bank = -1), "negative bank rejected atomically")
        Check(game.State.Main.Bank = 20 AndAlso game.UndoDescription = "right score", "failed validation changes nothing")
        Dim snapshot = game.State
        snapshot.Main.Bank = 500
        Check(game.State.Main.Bank = 20, "callers cannot mutate live state through snapshot")
        game.Execute("no award", Sub(s) GameRules.ShowMainAnswer(s.Main, 1, "Другой", 30, False))
        Check(game.State.Main.Bank = 20, "reveal without counting")
        game.Execute("count later", Sub(s) GameRules.ShowMainAnswer(s.Main, 1, "Другой", 30, True))
        Check(game.State.Main.Bank = 80, "previously uncounted answer can be counted once")
    End Sub

    Private Sub TestBigGame(directory As String)
        Dim game = Session(directory, "big")
        Check(game.State.Big.NextStep = 0, "initial big-game step")
        Reject(Sub() game.Execute("bad", Sub(s) GameRules.ShowBigPoints(s.Big, 0, 20)), "points cannot precede text")
        Check(game.State.Big.NextStep = 0 AndAlso game.UndoDescription = "", "failed step does not advance")
        For index = 0 To 9
            Dim i = index
            game.Execute("text " & i, Sub(s) GameRules.ShowBigText(s.Big, i, "Ответ " & i))
            Check(game.State.Big.NextStep = i * 2 + 1, "next step after text " & i)
            game.Execute("points " & i, Sub(s) GameRules.ShowBigPoints(s.Big, i, 20))
        Next
        Check(game.State.Big.NextStep = 20 AndAlso game.State.Big.Total = 200, "complete big-game progression")
        game.Execute("edit question 3", Sub(s) GameRules.ShowBigPoints(s.Big, 2, 35))
        Check(game.State.Big.Total = 215 AndAlso game.State.Big.Answers(2).Points = 35, "per-question correction updates total by difference")
        game.Undo()
        Check(game.State.Big.Total = 200 AndAlso game.State.Big.Answers(2).Points = 20, "undo per-question correction")
        game.Undo()
        Check(game.State.Big.Total = 180 AndAlso game.State.Big.NextStep = 19, "undo reveal returns step and total")
        game.Execute("manual total", Sub(s) GameRules.SetBigTotal(s.Big, 190))
        game.Execute("correct after manual", Sub(s) GameRules.ShowBigPoints(s.Big, 2, 25))
        Check(game.State.Big.Total = 195, "answer correction preserves manual adjustment")
        game.Execute("hide", Sub(s) s.Big.HideFirstPlayer = True)
        Check(game.State.Big.Answers(0).Text = "Ответ 0" AndAlso game.State.Big.Answers(0).ScoreVisible, "masking retains published values")
        game.Execute("new big", Sub(s) s.Big = New BigGameState())
        Check(game.State.Big.NextStep = 0 AndAlso game.State.Big.Total = 0 AndAlso Not game.State.Big.HideFirstPlayer, "reset whole big game")
        game.Undo()
        Check(game.State.Big.Total = 195 AndAlso game.State.Big.HideFirstPlayer, "undo new big game")
    End Sub

    Private Sub TestSplashes(directory As String)
        Dim game = Session(directory, "splashes")
        game.Execute("seed", Sub(s)
                                 s.Main.Round = 2
                                 s.Main.LeftScore = 42
                                 s.Main.RightScore = 71
                                 s.Main.LeftStrikes(0) = True
                                 GameRules.ShowMainAnswer(s.Main, 0, "Ответ", 10, True)
                                 GameRules.ShowBigText(s.Big, 0, "Первый")
                                 GameRules.ShowBigPoints(s.Big, 0, 30)
                                 s.Big.HideFirstPlayer = True
                             End Sub)
        For i = 0 To 5
            Dim preset = SplashPresets.Create(i)
            game.Execute("presets", Sub(s)
                                        GameRules.ShowSplash(s.Main.SplashScreen, preset.Line1, preset.Line2)
                                    End Sub)
            Dim state = game.State
            Check(state.Main.SplashScreen.Line1 = preset.Line1 AndAlso state.Main.SplashScreen.Line2 = preset.Line2, "main game accepts preset " & i)
            Check(state.Main.Round = 2 AndAlso state.Main.Bank = 20 AndAlso state.Main.LeftScore = 42 AndAlso state.Main.RightScore = 71 AndAlso state.Main.LeftStrikes(0), "splash preserves main game " & i)
            Check(state.Big.Total = 30 AndAlso state.Big.NextStep = 2 AndAlso state.Big.HideFirstPlayer, "splash preserves big game " & i)
            game.Undo()
            Check(Not game.State.Main.SplashScreen.Visible, "undo removes main overlay " & i)
        Next
        game.Execute("custom", Sub(s)
                                   GameRules.ShowSplash(s.Main.SplashScreen, "  ПАУЗА  ", "ПРОДОЛЖЕНИЕ & СТАРТ")
                               End Sub)
        Check(game.State.Main.SplashScreen.Line1 = "ПАУЗА", "splash trims surrounding whitespace")
        For Each invalid In New String() {"", " ", New String("Я"c, 61), "ОДНА" & vbLf & "ДВЕ", "ОДНА" & vbCr & "ДВЕ"}
            Reject(Sub() game.Execute("bad splash", Sub(s) GameRules.ShowSplash(s.Main.SplashScreen, invalid, "ИГРА")), "reject invalid splash line")
            Check(game.UndoDescription = "custom" AndAlso game.State.Main.SplashScreen.Line1 = "ПАУЗА", "bad splash changes neither state nor undo")
        Next
        Dim maximum As New SplashState()
        GameRules.ShowSplash(maximum, New String("Ш"c, 60), "ИГРА")
        Check(maximum.Line1.Length = 60, "60-character splash accepted")
        game.Execute("return", Sub(s) s.Main.SplashScreen.Visible = False)
        Dim restored = Session(directory, "splashes")
        restored.Restore()
        Check(Not restored.State.Main.SplashScreen.Visible AndAlso restored.State.Main.SplashScreen.Line2 = "ПРОДОЛЖЕНИЕ & СТАРТ", "restore custom text and independent visibility")
        restored.Undo()
        Check(restored.State.Main.SplashScreen.Visible, "undo return after restart")
        restored.Undo()
        Check(Not restored.State.Main.SplashScreen.Visible AndAlso restored.State.Big.Total = 30, "undo custom text after restart")
        game.Execute("show for correction", Sub(s) GameRules.ShowSplash(s.Main.SplashScreen, "ПАУЗА", "ИГРА"))
        game.Execute("correct visible", Sub(s)
                                            GameRules.EditMainAnswer(s.Main, 0, "Исправленный", 11)
                                            GameRules.ShowBigText(s.Big, 0, "Исправленный")
                                            GameRules.ShowBigPoints(s.Big, 0, 31)
                                        End Sub)
        Check(game.State.Main.SplashScreen.Visible, "corrections keep current display mode")
        game.Execute("show again", Sub(s) GameRules.ShowSplash(s.Main.SplashScreen, "ПАУЗА", "ИГРА"))
        game.Execute("open new", Sub(s)
                                     GameRules.ShowMainAnswer(s.Main, 1, "Новый", 5, True)
                                     GameRules.ShowBigText(s.Big, 1, "Новый")
                                 End Sub)
        Check(Not game.State.Main.SplashScreen.Visible AndAlso game.State.Main.Bank = 32, "new answers restore field and award once")
        game.Undo()
        Check(game.State.Main.SplashScreen.Visible AndAlso game.State.Main.Bank = 22, "undo restores splash with original bank")
        game.Execute("text before price", Sub(s) GameRules.ShowBigText(s.Big, 1, "Новый"))
        game.Execute("first price", Sub(s) GameRules.ShowBigPoints(s.Big, 1, 10))
        Check(game.State.Main.SplashScreen.Visible AndAlso game.State.Big.Total = 41, "big price reveal preserves main splash")
        game.Undo()
        Check(game.State.Main.SplashScreen.Visible AndAlso game.State.Big.Total = 31, "undo big price preserves main splash")
        Check(Not File.ReadAllText(Path.Combine(directory, "splashes", "game.xml")).Contains("<Splash>"), "new XML omits retired preset tag")
    End Sub

    Private Sub TestLegacySplashes(directory As String)
        For index = -1 To 5
            Dim target = Path.Combine(directory, "legacy-splash-" & index)
            IO.Directory.CreateDirectory(target)
            Dim saved As New SavedMatch With {.History = New List(Of UndoEntry) From {New UndoEntry With {.Description = "old action", .State = New MatchState()}}}
            saved.Current.Main.LeftScore = 123
            Dim xml As New Xml.XmlDocument()
            Using writer As New StringWriter()
                Dim serializer As New Xml.Serialization.XmlSerializer(GetType(SavedMatch))
                serializer.Serialize(writer, saved)
                xml.LoadXml(writer.ToString())
            End Using
            For Each snapshot As Xml.XmlNode In xml.SelectNodes("//Current | //UndoEntry/State")
                snapshot.SelectSingleNode("FormatVersion").InnerText = "1"
                Dim main = snapshot.SelectSingleNode("Main")
                main.RemoveChild(main.SelectSingleNode("SplashScreen"))
                Dim legacy = xml.CreateElement("Splash")
                legacy.InnerText = If(snapshot.Name = "Current", index, 5).ToString()
                main.AppendChild(legacy)
            Next
            xml.Save(Path.Combine(target, "game.xml"))
            Dim restored As New GameSession(target)
            AddHandler restored.StorageError, Sub(message) _storageErrors.Add(message)
            restored.Restore()
            Dim state = restored.State
            Check(state.FormatVersion = 2 AndAlso state.Main.LeftScore = 123 AndAlso state.Main.SplashScreen.Visible = (index >= 0), "legacy main display migrates " & index)
            Check(state.Main.SplashScreen.Line1 = SplashPresets.Create(Math.Max(0, index)).Line1 AndAlso state.Big.Total = 0, "legacy preset and big-game data " & index)
            restored.Undo()
            Check(restored.State.Main.SplashScreen.Line1 = "БОЛЬШАЯ" AndAlso restored.State.Main.SplashScreen.Visible, "legacy undo snapshots also migrate " & index)
            xml.SelectSingleNode("//Current/FormatVersion").InnerText = "99"
            xml.Save(Path.Combine(target, "game.xml"))
            Reject(Sub() restored.Restore(), "unsupported save version rejected")
            Check(restored.State.Main.SplashScreen.Line1 = "БОЛЬШАЯ", "bad save does not replace live state")
        Next
    End Sub

    Private Sub TestRetiredBigSplash(directory As String)
        Dim target = Path.Combine(directory, "retired-big-splash")
        IO.Directory.CreateDirectory(target)
        Dim saved As New SavedMatch()
        GameRules.ShowBigText(saved.Current.Big, 0, "СЕМЬЯ")
        GameRules.ShowBigPoints(saved.Current.Big, 0, 45)
        saved.History.Add(New UndoEntry With {.Description = "old big splash", .State = GameRules.Clone(saved.Current)})
        Dim xml As New Xml.XmlDocument()
        Using writer As New StringWriter()
            Dim serializer As New Xml.Serialization.XmlSerializer(GetType(SavedMatch))
            serializer.Serialize(writer, saved)
            xml.LoadXml(writer.ToString())
        End Using
        For Each big As Xml.XmlNode In xml.SelectNodes("//Big")
            Dim obsolete = xml.CreateElement("SplashScreen")
            obsolete.InnerXml = "<Visible>true</Visible><Line1>БОЛЬШАЯ</Line1><Line2>ИГРА</Line2>"
            big.AppendChild(obsolete)
        Next
        xml.Save(Path.Combine(target, "game.xml"))
        Dim restored As New GameSession(target)
        AddHandler restored.StorageError, Sub(message) _storageErrors.Add(message)
        restored.Restore()
        Check(restored.State.Big.Total = 45 AndAlso restored.State.Big.Answers(0).TextVisible, "retired big splash does not hide restored answers")
        restored.Undo()
        Check(restored.State.Big.Total = 45 AndAlso restored.State.Big.NextStep = 2, "retired big splash history preserves gameplay")
        xml.Load(Path.Combine(target, "game.xml"))
        Check(xml.SelectSingleNode("//Big/SplashScreen") Is Nothing, "new saves omit retired big splash data")
    End Sub

    Private Sub TestBuzzer()
        ' Чистый арбитраж: удержание, отпускание, победитель и ничья одного отчёта.
        ' HID-дескриптор, USB-соединение и лампы проверяются отдельно на приёмнике.
        Dim gate As New BuzzerGate()
        Check(gate.Accept(1) = -1, "disarmed buttons ignored")
        gate.Arm()
        Check(gate.Accept(1) = -1, "held button is not a fresh press")
        Check(gate.Accept(0) = -1 AndAlso gate.Armed, "release makes next press eligible")
        Check(gate.Accept(8) = 3 AndAlso Not gate.Armed, "fourth pad can win")
        Check(gate.Accept(2) = -1, "winner locks all pads")
        gate.Accept(0) : gate.Arm()
        Check(gate.Accept(3) = -2 AndAlso Not gate.Armed, "same-report tie has no index bias")
        gate.Accept(0) : gate.Arm() : gate.Disarm()
        Check(gate.Accept(1) = -1, "explicit disarm")
    End Sub

    Private NotInheritable Class FakeReceiver
        Implements IBuzzReceiver
        Public ReadOnly Inputs As New BlockingCollection(Of Integer)()
        Public Failure As Exception
        Public LampWorks As Boolean = True
        Public DisposedCount As Integer
        Public BeforeRead As Action

        Public Function ReadButtons() As Integer? Implements IBuzzReceiver.ReadButtons
            If BeforeRead IsNot Nothing Then BeforeRead()
            If Failure IsNot Nothing Then Throw Failure
            Dim mask As Integer
            If Inputs.TryTake(mask, 20) Then Return mask
            Return Nothing
        End Function
        Public Function SetLamp(pad As Integer, lit As Boolean) As Boolean Implements IBuzzReceiver.SetLamp
            Return LampWorks
        End Function
        Public Sub Dispose() Implements IDisposable.Dispose
            Interlocked.Increment(DisposedCount)
        End Sub
    End Class

    Private Sub WaitFor(condition As Func(Of Boolean), description As String, Optional ui As Boolean = False)
        Dim watch = Diagnostics.Stopwatch.StartNew()
        While Not condition() AndAlso watch.ElapsedMilliseconds < 3000
            If ui Then Application.DoEvents()
            Thread.Sleep(5)
        End While
        Check(condition(), description)
    End Sub

    Private Sub TestBuzzConnection()
        ' Реальный менеджер и его события, но управляемый источник HID-отчётов.
        Using missing As New BuzzManager(Function() Nothing)
            missing.ConnectAsync().GetAwaiter().GetResult()
            Check(missing.Status.State = BuzzState.Disconnected AndAlso missing.Status.CanConnect, "missing receiver is a normal state")
            Reject(Sub() missing.Arm(), "cannot arm missing receiver")
            missing.Disarm()
            Check(missing.Status.State = BuzzState.Disconnected, "round reset cannot invent connection")
            missing.ShutdownAsync().GetAwaiter().GetResult()
        End Using
        Using opened As New ManualResetEventSlim(), release As New ManualResetEventSlim()
            Dim first As New FakeReceiver With {.LampWorks = False}
            Dim second As New FakeReceiver()
            Dim attempts As Integer
            Using manager As New BuzzManager(Function()
                                                If Interlocked.Increment(attempts) = 1 Then
                                                    opened.Set()
                                                    release.Wait()
                                                    Return first
                                                End If
                                                Return second
                                            End Function)
                Dim connecting = manager.ConnectAsync()
                Check(opened.Wait(2000) AndAlso Not connecting.IsCompleted, "slow USB search runs outside caller")
                Check(manager.Status.State = BuzzState.Connecting AndAlso Not manager.Status.CanArm AndAlso Not manager.Status.CanConnect, "commands disabled while connecting")
                manager.ConnectAsync().GetAwaiter().GetResult()
                Check(attempts = 1, "repeated click cannot start concurrent search")
                release.Set()
                connecting.GetAwaiter().GetResult()
                WaitFor(Function() manager.Status.Warning <> "", "lamp failure reported")
                Check(manager.Status.CanArm AndAlso manager.Status.State = BuzzState.Ready, "lamp failure does not disable input")
                first.LampWorks = True
                manager.Arm()
                WaitFor(Function() manager.Status.Warning = "", "lamp recovery clears warning")
                first.Inputs.Add(8)
                WaitFor(Function() manager.Status.State = BuzzState.Winner, "manager accepts fourth pad")
                Check(manager.Status.WinningPad = 3 AndAlso manager.Status.CanArm AndAlso Not manager.Status.CanDisarm, "winner locks input but allows explicit next round")
                manager.Arm()
                first.Inputs.Add(8)
                Thread.Sleep(40)
                Check(manager.Status.State = BuzzState.Armed, "held winning button cannot win new round")
                first.Inputs.Add(0)
                first.Inputs.Add(3)
                WaitFor(Function() manager.Status.State = BuzzState.Tie, "manager reports same-report tie")
                first.Failure = New IOException("test unplugged")
                WaitFor(Function() manager.Status.State = BuzzState.Faulted, "unplug blocks hardware commands")
                Check(manager.Status.CanConnect AndAlso Not manager.Status.CanArm AndAlso Not manager.Status.CanDisarm, "unplug availability")
                manager.Disarm()
                Check(manager.Status.State = BuzzState.Faulted, "disarm after unplug preserves fault")
                Reject(Sub() manager.Arm(), "arm rechecks connection after unplug")
                manager.ConnectAsync().GetAwaiter().GetResult()
                Check(manager.Status.State = BuzzState.Ready AndAlso Not manager.Status.CanDisarm, "reconnect never rearms automatically")
                Check(first.DisposedCount = 1, "old receiver disposed before new connection")
                manager.ShutdownAsync().GetAwaiter().GetResult()
                Check(second.DisposedCount = 1 AndAlso manager.Status.State = BuzzState.Disposed, "shutdown releases current receiver")
            End Using
        End Using
        Using release As New ManualResetEventSlim(), entered As New ManualResetEventSlim()
            Dim receiver As New FakeReceiver()
            Using manager As New BuzzManager(Function()
                                                entered.Set()
                                                release.Wait()
                                                Return receiver
                                            End Function)
                manager.ConnectAsync()
                Check(entered.Wait(2000), "shutdown test entered search")
                Dim stopping = manager.ShutdownAsync()
                Check(Not stopping.IsCompleted AndAlso manager.Status.State = BuzzState.Disposed, "shutdown during search returns without blocking caller")
                release.Set()
                stopping.GetAwaiter().GetResult()
                Check(receiver.DisposedCount = 1 AndAlso manager.Status.State = BuzzState.Disposed, "cancelled search releases receiver without publishing ready")
            End Using
        End Using
    End Sub

    Private Sub TestScoreDialog(owner As Form, commit As Boolean, value As Integer, Optional buttonName As String = "CorrectScore0")
        owner.BeginInvoke(New Action(Sub()
                                        Dim dialog = Application.OpenForms.Cast(Of Form)().Single(Function(form) form.Name = "NumberDialog")
                                        DirectCast(FindControl(dialog, "NewValue"), NumericUpDown).Value = value
                                        If commit Then
                                            DirectCast(FindControl(dialog, "ApplyNumber"), Button).PerformClick()
                                        Else
                                            dialog.DialogResult = DialogResult.Cancel
                                        End If
                                    End Sub))
        DirectCast(FindControl(owner, buttonName), Button).PerformClick()
    End Sub

    Private Sub TestPersistence(directory As String)
        ' История должна переживать перезапуск, а повреждённый XML — не заменять
        ' открытую игру. Для File.Replace нужны права на замену тестового файла.
        Dim game = Session(directory, "persist")
        game.Execute("left 42", Sub(s) s.Main.LeftScore = 42)
        game.Execute("big answer", Sub(s) GameRules.ShowBigText(s.Big, 7, "Сохранённый ответ"))
        Dim restored = Session(directory, "persist")
        restored.Restore()
        Check(restored.State.Main.LeftScore = 42 AndAlso restored.State.Big.Answers(7).TextVisible, "restore both games")
        Check(restored.UndoDescription = "big answer", "restore history")
        restored.Undo()
        Check(Not restored.State.Big.Answers(7).TextVisible AndAlso restored.State.Main.LeftScore = 42, "undo after restore")
        Dim path = IO.Path.Combine(directory, "persist", "game.xml")
        File.WriteAllText(path, "<broken>")
        Dim failed As Boolean
        Try
            restored.Restore()
        Catch ex As InvalidOperationException
            failed = True
        End Try
        Check(failed AndAlso restored.State.Main.LeftScore = 42, "corrupt save cannot replace live state")
        Dim bounded = Session(directory, "history")
        For i = 1 To 105
            Dim value = i
            bounded.Execute("score", Sub(s) s.Main.LeftScore = value)
        Next
        For i = 1 To 100
            bounded.Undo()
        Next
        Check(bounded.State.Main.LeftScore = 5 AndAlso bounded.UndoDescription = "", "history capped at 100 actions")
    End Sub

    Private Sub TestUi(directory As String)
        ' Проверяем обработчики кнопок и шрифты; это не проверка аудиовыхода/проектора.
        Application.EnableVisualStyles()
        AudioService.Instance.Enabled = False
        Environment.SetEnvironmentVariable("STOKODNOMU_DATA_DIR", Path.Combine(directory, "ui"))
        Dim uiReceiver As FakeReceiver = Nothing
        Using buzz As New BuzzManager(Function() uiReceiver), main As New Form1(buzz), countdown As New Countdown()
            Dim game = Session(directory, "ui-big")
            Using big As New BigGameControl(game, countdown, Sub() Return, Sub() game.Undo(), Sub() Return, Sub() Return, Sub() Return), board As New Tablo(), bigBoard As New BigGameTablo()
                For Each form As Form In New Form() {main, big, board, bigBoard}
                    ' Show создаёт layout/контролы; непоказанная форма давала пустой DrawToBitmap.
                    ' Прозрачность скрывает тестовые окна от пользователя.
                    form.ShowInTaskbar = False
                    form.Opacity = 0
                    form.Show()
                    Application.DoEvents()
                    If form Is main Then
                        Check(FindControl(main, "ConnectBuzz").Enabled AndAlso Not FindControl(main, "ArmBuzz").Enabled AndAlso Not FindControl(main, "BlockBuzz").Enabled, "missing receiver disables only hardware commands")
                        uiReceiver = New FakeReceiver()
                        DirectCast(FindControl(main, "ConnectBuzz"), Button).PerformClick()
                        WaitFor(Function() FindControl(main, "ArmBuzz").Enabled, "UI connected and locked", True)
                        DirectCast(FindControl(main, "ArmBuzz"), Button).PerformClick()
                        Check(Not FindControl(main, "ArmBuzz").Enabled AndAlso FindControl(main, "BlockBuzz").Enabled, "UI arm updates availability")
                        uiReceiver.Inputs.Add(1)
                        WaitFor(Function() FindControl(main, "ArmBuzz").Enabled, "UI winner allows next round", True)
                        uiReceiver.Failure = New IOException("test unplugged")
                        WaitFor(Function() FindControl(main, "ConnectBuzz").Enabled, "UI unplug restores connect command", True)
                        Check(Not FindControl(main, "ArmBuzz").Enabled AndAlso Not FindControl(main, "BlockBuzz").Enabled, "UI unplug blocks arm and disarm")
                        uiReceiver = Nothing
                        DirectCast(FindControl(main, "ConnectBuzz"), Button).PerformClick()
                        WaitFor(Function() FindControl(main, "BuzzStatus").Text = "Не подключён", "UI retry without receiver remains disconnected", True)
                        DirectCast(FindControl(main, "MainAnswer0"), TextBox).Text = "Проверочный ответ"
                        DirectCast(FindControl(main, "MainPoints0"), NumericUpDown).Value = 10
                        DirectCast(FindControl(main, "ToggleMainAnswer0"), Button).PerformClick()
                        Check(Integer.Parse(FindControl(main, "RoundBank").Text) = 10, "main operator reveal updates bank")
                        DirectCast(FindControl(main, "ToggleMainAnswer0"), Button).PerformClick()
                        DirectCast(FindControl(main, "ToggleMainAnswer0"), Button).PerformClick()
                        Check(Integer.Parse(FindControl(main, "RoundBank").Text) = 10, "main operator hide/reopen preserves bank")
                        DirectCast(FindControl(main, "MainPoints0"), NumericUpDown).Value = 15
                        DirectCast(FindControl(main, "EditMainAnswer0"), Button).PerformClick()
                        Check(Integer.Parse(FindControl(main, "RoundBank").Text) = 15, "main operator applies correction")
                        DirectCast(FindControl(main, "UndoLastAction"), Button).PerformClick()
                        Check(Integer.Parse(FindControl(main, "RoundBank").Text) = 10, "main operator undo works")
                        TestScoreDialog(main, False, 120)
                        Check(FindControl(main, "LeftScore").Text = "0", "cancel score dialog leaves match unchanged")
                        TestScoreDialog(main, True, 120)
                        Check(FindControl(main, "LeftScore").Text = "120", "score dialog commits selected team")
                        DirectCast(FindControl(main, "UndoLastAction"), Button).PerformClick()
                        Check(FindControl(main, "LeftScore").Text = "0", "score dialog action can be undone")
                        TestScoreDialog(main, True, GameRules.MaximumScore)
                        Dim scoreLabel = FindControl(main, "LeftScore")
                        Check(TextRenderer.MeasureText(scoreLabel.Text, scoreLabel.Font, Size.Empty, TextFormatFlags.NoPadding).Width <= scoreLabel.ClientSize.Width, "six-digit score fits without ellipsis")
                        DirectCast(FindControl(main, "UndoLastAction"), Button).PerformClick()
                        Dim editor = DirectCast(FindControl(main, "MainAnswer1"), TextBox)
                        editor.Focus()
                        Dim key As New KeyEventArgs(Keys.Control Or Keys.Z)
                        GetType(Form).GetMethod("OnKeyDown", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).Invoke(main, New Object() {key})
                        Check(Not key.SuppressKeyPress AndAlso Integer.Parse(FindControl(main, "RoundBank").Text) = 10, "Ctrl+Z in answer editor does not undo match")
                        Check(OperatorUi.EditingText(FindControl(main, "MainPoints0")), "number editor also retains text undo")
                        Dim strike = DirectCast(FindControl(main, "LeftStrike0"), Button)
                        Check(strike.Text = "× 1" AndAlso strike.AccessibleDescription = "Промаха нет", "inactive strike uses cross")
                        strike.PerformClick()
                        Check(strike.Text = "× 1" AndAlso strike.AccessibleDescription = "Промах поставлен" AndAlso strike.BackColor = Color.MistyRose, "active strike retains cross and changes state")
                        DirectCast(FindControl(main, "UndoLastAction"), Button).PerformClick()
                        Check(strike.AccessibleDescription = "Промаха нет", "strike undo preserves button symbol")
                        Dim mainSession = DirectCast(GetType(Form1).GetField("_session", Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Instance).GetValue(main), GameSession)
                        TestSplashEditor(main, mainSession, directory, "Main")
                    ElseIf form Is big Then
                        TestBigTabOrder(big)
                        DirectCast(FindControl(big, "BigAnswer0"), TextBox).Text = "Первый ответ"
                        DirectCast(FindControl(big, "BigPoints0"), NumericUpDown).Value = 50
                        DirectCast(FindControl(big, "BigAnswer1"), TextBox).Text = "Черновик второго ответа"
                        DirectCast(FindControl(big, "RevealNext"), Button).PerformClick()
                        DirectCast(FindControl(big, "RevealNext"), Button).PerformClick()
                        Check(game.State.Big.Total = 50 AndAlso game.State.Big.NextStep = 2, "big operator next reveals text then points")
                        Check(DirectCast(FindControl(big, "BigAnswer1"), TextBox).Text <> "", "revealing one row preserves other drafts")
                        DirectCast(FindControl(big, "BigPoints0"), NumericUpDown).Value = 70
                        DirectCast(FindControl(big, "ShowBigPoints0"), Button).PerformClick()
                        Check(game.State.Big.Total = 70, "big operator per-question correction")
                        game.Undo()
                        Check(game.State.Big.Total = 50 AndAlso DirectCast(FindControl(big, "BigPoints0"), NumericUpDown).Value = 50, "big operator correction undo")
                        TestScoreDialog(big, True, 80, "CorrectBigTotal")
                        Check(game.State.Big.Total = 80, "big total dialog applies manual adjustment")
                        game.Undo()
                        Check(game.State.Big.Total = 50, "big total dialog undo")
                        TestScoreDialog(big, True, 200, "CorrectBigTarget")
                        Check(game.State.Big.TargetScore = 200, "big target dialog applies value")
                        game.Undo()
                        DirectCast(FindControl(big, "SaveBigDrafts"), Button).PerformClick()
                        Check(game.State.Big.Answers(1).Text = "Черновик второго ответа" AndAlso Not game.State.Big.Answers(1).TextVisible AndAlso game.State.Big.Total = 50, "save drafts never reveals or counts them")
                        DirectCast(FindControl(big, "Start20"), Button).PerformClick()
                        Check(countdown.SecondsRemaining = 20, "big UI starts 20 seconds")
                        DirectCast(FindControl(big, "Start25"), Button).PerformClick()
                        Check(countdown.SecondsRemaining = 25, "big UI restarts at 25 seconds")
                        DirectCast(FindControl(big, "BigStopAllAudio"), Button).PerformClick()
                        Check(Not countdown.Running AndAlso game.State.Big.Total = 50, "stop all keeps score")
                        Check(FindControl(big, "SplashPresets") Is Nothing AndAlso FindControl(big, "PreviewSplash") Is Nothing, "big operator has no splash controls")
                    End If
                    If form Is main OrElse form Is big Then
                        Check(Not form.MaximizeBox, "operator maximize button disabled: " & form.GetType().Name)
                        form.ClientSize = New Size(1280, 720)
                        form.PerformLayout()
                        Application.DoEvents()
                        Dim scroll = DirectCast(FindControl(form, "ConsoleScroll"), Panel)
                        Check(Not scroll.HorizontalScroll.Visible, "no horizontal scroll at 1280: " & form.GetType().Name)
                        Dim lastCommand = FindControl(form, If(form Is main, "ToggleMainAnswer5", "ShowBigPoints9"))
                        Dim lastBottom = scroll.PointToClient(lastCommand.PointToScreen(New Point(0, lastCommand.Height))).Y
                        Check(lastBottom <= scroll.ClientSize.Height, "last answer action fits viewport: " & form.GetType().Name)
                        Dim footer = FindControl(form, "ConsoleFooter")
                        Dim bottom = form.PointToClient(footer.PointToScreen(New Point(0, footer.Height))).Y
                        Check(bottom <= form.ClientSize.Height, "footer remains in client area: " & form.GetType().Name)
                        Check(Not AllControls(form).OfType(Of Label)().Any(Function(label) label.Text.StartsWith("Последнее действие:") OrElse label.Text.Contains("Автосохранение: сохранено")), "no routine event feed")
                    End If
                    Dim handle = form.Handle
                    form.PerformLayout()
                    Using bitmap As New Bitmap(form.Width, form.Height)
                        Using graphics = System.Drawing.Graphics.FromImage(bitmap)
                            For Each label In AllControls(form).OfType(Of Label)()
                                Check(label.Font.GetHeight(graphics) > 0, "valid label font: " & label.Name)
                            Next
                        End Using
                        form.DrawToBitmap(bitmap, New Rectangle(Point.Empty, bitmap.Size))
                        bitmap.Save(Path.Combine(directory, form.GetType().Name & ".png"))
                    End Using
                    Check(form.Width > 600 AndAlso form.Controls.Count > 0, "render " & form.GetType().Name)
                    form.Hide()
                Next
                Check(FindControl(main, "MainAnswer0") IsNot Nothing, "operator answer editor exists")
                Check(FindControl(big, "BigPoints9") IsNot Nothing, "per-question editor exists for second player")
                TestSplashRendering(board, bigBoard, directory)
                TestStrikeScaling(board, directory)
            End Using
            main.Show()
            Using entered As New ManualResetEventSlim(), release As New ManualResetEventSlim()
                uiReceiver = New FakeReceiver With {.BeforeRead = Sub()
                                                                    entered.Set()
                                                                    release.Wait()
                                                                End Sub}
                Try
                    DirectCast(FindControl(main, "ConnectBuzz"), Button).PerformClick()
                    WaitFor(Function() entered.IsSet, "exit test entered pending USB read", True)
                    Dim rootTimer = DirectCast(GetType(Form1).GetField("_countdown", Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Instance).GetValue(main), Countdown)
                    rootTimer.Start(25)
                    TestExitDialog(main, False, Path.Combine(directory, "ExitConfirmation.png"))
                    Check(Not main.IsDisposed AndAlso main.Enabled AndAlso rootTimer.Running AndAlso buzz.Status.State = BuzzState.Ready, "declined exit leaves app and services running")
                    TestExitDialog(main, True, Nothing)
                    Check(Not main.IsDisposed AndAlso Not main.Enabled AndAlso Not rootTimer.Running, "confirmed exit stops timer and waits for receiver")
                    Dim responsive As Boolean
                    main.BeginInvoke(New Action(Sub() responsive = True))
                    WaitFor(Function() responsive, "UI remains responsive during graceful shutdown", True)
                    ' Повторный Close в ожидании USB не открывает новый диалог.
                    main.Close()
                    Check(Not Application.OpenForms.Cast(Of Form)().Any(Function(form) form.Name = "ExitConfirmation"), "shutdown never asks twice")
                    release.Set()
                    WaitFor(Function() main.IsDisposed, "graceful exit finishes after receiver releases", True)
                    Check(uiReceiver.DisposedCount = 1 AndAlso buzz.Status.State = BuzzState.Disposed, "graceful exit disposes receiver once")
                Finally
                    release.Set()
                End Try
            End Using
        End Using
        Using fastBuzz As New BuzzManager(Function() Nothing), fast As New Form1(fastBuzz)
            fast.ShowInTaskbar = False
            fast.Opacity = 0
            fast.Show()
            WaitFor(Function() fastBuzz.Status.State = BuzzState.Disconnected, "exit without receiver finishes initial search", True)
            TestExitDialog(fast, True, Nothing)
            WaitFor(Function() fast.IsDisposed, "confirmed exit without receiver closes on next UI turn", True)
        End Using
        Console.WriteLine("UI snapshots: " & directory)
    End Sub

    Private Sub TestExitDialog(main As Form1, accept As Boolean, snapshotPath As String)
        main.BeginInvoke(New Action(Sub()
                                        Dim dialog = Application.OpenForms.Cast(Of Form)().Single(Function(form) form.Name = "ExitConfirmation")
                                        dialog.Opacity = 0
                                        Check(FindControl(dialog, "ExitQuestion").Text = "Закрыть приложение?", "exit confirmation question")
                                        Check(DirectCast(dialog.AcceptButton, Button).Text = "Нет", "exit defaults to keeping app open")
                                        Check(dialog.ActiveControl Is FindControl(dialog, "ConfirmExitNo"), "exit keyboard focus starts on No")
                                        If snapshotPath IsNot Nothing Then
                                            dialog.PerformLayout()
                                            Using bitmap As New Bitmap(dialog.Width, dialog.Height)
                                                dialog.DrawToBitmap(bitmap, New Rectangle(Point.Empty, bitmap.Size))
                                                bitmap.Save(snapshotPath)
                                            End Using
                                        End If
                                        DirectCast(FindControl(dialog, If(accept, "ConfirmExitYes", "ConfirmExitNo")), Button).PerformClick()
                                    End Sub))
        main.Close()
    End Sub

    Private Function Card(state As MatchState) As SplashState
        Return state.Main.SplashScreen
    End Function

    Private Sub TestSplashEditor(form As Form, game As GameSession, directory As String, prefix As String)
        Dim before = game.State
        Dim description = game.UndoDescription
        Dim presets = DirectCast(FindControl(form, "SplashPresets"), ComboBox)
        For i = 0 To 5
            presets.SelectedIndex = i
            Dim preset = SplashPresets.Create(i)
            Check(FindControl(form, "SplashLine1").Text = preset.Line1 AndAlso FindControl(form, "SplashLine2").Text = preset.Line2, "editor fills preset " & prefix & i)
            Check(game.UndoDescription = description AndAlso Card(game.State).Line1 = Card(before).Line1, "preset selection never publishes " & prefix & i)
        Next
        FindControl(form, "SplashLine1").Text = "ПАУЗА"
        FindControl(form, "SplashLine2").Text = "ПРОДОЛЖЕНИЕ"
        Check(presets.Items.Count = 6 AndAlso presets.SelectedIndex = -1, "custom text retains all presets " & prefix)
        form.BeginInvoke(New Action(Sub()
                                        Dim preview = Application.OpenForms.Cast(Of Form)().Single(Function(item) item.Name = "SplashPreview")
                                        preview.Opacity = 0
                                        Check(FindControl(preview, "SplashOverlay").Text = "ПАУЗА" & vbLf & "ПРОДОЛЖЕНИЕ", "preview shows draft " & prefix)
                                        SaveBitmap(preview, Path.Combine(directory, prefix & "SplashPreview.png"))
                                        preview.Close()
                                    End Sub))
        DirectCast(FindControl(form, "PreviewSplash"), Button).PerformClick()
        Check(game.UndoDescription = description AndAlso Card(game.State).Visible = Card(before).Visible, "preview leaves session untouched " & prefix)
        DirectCast(FindControl(form, "ShowSplash"), Button).PerformClick()
        Check(Card(game.State).Visible AndAlso Card(game.State).Line1 = "ПАУЗА", "publish custom text " & prefix)
        FindControl(form, "SplashLine1").Text = "ЧЕРНОВИК"
        game.Execute("unrelated", Sub(s) s.Main.RightScore += 1)
        Check(FindControl(form, "SplashLine1").Text = "ЧЕРНОВИК", "unrelated command preserves splash draft " & prefix)
        game.Undo()
        DirectCast(FindControl(form, "ReturnAnswers"), Button).PerformClick()
        Check(Not Card(game.State).Visible, "return button hides overlay " & prefix)
        game.Undo()
        Check(Card(game.State).Visible, "undo return restores overlay " & prefix)
        game.Undo()
        Check(Card(game.State).Line1 = Card(before).Line1 AndAlso Card(game.State).Visible = Card(before).Visible, "undo publication restores prior card " & prefix)
        Check(game.State.Main.Bank = before.Main.Bank AndAlso game.State.Big.Total = before.Big.Total, "splash editor does not change scores " & prefix)
    End Sub

    Private Sub SaveBitmap(form As Form, path As String)
        form.PerformLayout()
        Using bitmap As New Bitmap(form.Width, form.Height)
            form.DrawToBitmap(bitmap, New Rectangle(Point.Empty, bitmap.Size))
            bitmap.Save(path)
        End Using
    End Sub

    Private Sub TestSplashRendering(main As Tablo, big As BigGameTablo, directory As String)
        main.Show()
        big.Show()
        Dim state As New MatchState()
        state.Main.Round = 2
        state.Main.LeftScore = 100
        state.Main.RightScore = 200
        state.Main.LeftStrikes(0) = True
        GameRules.ShowMainAnswer(state.Main, 0, "СЕМЬЯ", 40, True)
        GameRules.ShowBigText(state.Big, 0, "СЕМЬЯ")
        GameRules.ShowBigPoints(state.Big, 0, 50)
        state.Big.HideFirstPlayer = False
        For i = 0 To 5
            Dim preset = SplashPresets.Create(i)
            GameRules.ShowSplash(state.Main.SplashScreen, preset.Line1, preset.Line2)
            main.Render(state.Main)
            big.Render(state.Big)
            big.RenderTimer(17, True)
            Check(FindControl(main, "SplashOverlay").Visible AndAlso FindControl(main, "SplashOverlay").Text = preset.Line1 & vbLf & preset.Line2, "main overlay preset " & i)
            Check(FindControl(big, "SplashOverlay") Is Nothing AndAlso FindControl(big, "Answer1Lbl").Visible AndAlso FindControl(big, "Answer1ScoreLbl").Text = "50", "main preset leaves big gameplay visible " & i)
            Check(Not FindControl(main, "Answer1Lbl").Visible AndAlso Not FindControl(main, "Answer1ScoreLbl").Visible, "splash masks texts and prices " & i)
            Check(FindControl(main, "ScoreLeftLbl").Visible AndAlso FindControl(main, "LeftStrike1Lbl").Visible AndAlso FindControl(big, "TotalLbl").Visible AndAlso FindControl(big, "TimerLbl").Visible, "scores strikes and countdown remain visible " & i)
            If i = 2 Then SaveBitmap(main, Path.Combine(directory, "MainSplash.png"))
            If i = 5 Then SaveBitmap(big, Path.Combine(directory, "BigGameplay.png"))
        Next
        GameRules.ShowSplash(state.Main.SplashScreen, New String("Ш"c, 60), New String("Я"c, 60))
        main.Render(state.Main)
        main.ClientSize = New Size(640, 380)
        SaveBitmap(main, Path.Combine(directory, "LongSplash.png"))
        main.ClientSize = New Size(1264, 681)
        state.Main.SplashScreen.Visible = False
        state.Big.HideFirstPlayer = True
        main.Render(state.Main)
        big.Render(state.Big)
        Check(FindControl(main, "Answer1Lbl").Visible AndAlso FindControl(main, "Answer1ScoreLbl").Text = "40", "main return restores answer and base price")
        Check(FindControl(big, "Answer1Lbl").Visible AndAlso FindControl(big, "Answer1Lbl").Text = "••••••••" AndAlso FindControl(big, "TimerLbl").Text = "17", "big return restores mask and preserves countdown")
        SaveBitmap(main, Path.Combine(directory, "MainReturned.png"))
        SaveBitmap(big, Path.Combine(directory, "BigReturned.png"))
        main.Hide()
        big.Hide()
    End Sub

    Private Sub TestStrikeScaling(main As Tablo, directory As String)
        main.Show()
        Dim state As New MainGameState With {.Round = 2, .LeftScore = 100, .RightScore = 200}
        state.SplashScreen.Visible = False
        state.LeftStrikes = {True, False, True}
        state.RightStrikes = {False, True, False}
        GameRules.ShowMainAnswer(state, 0, "СЕМЬЯ", 40, True)
        Using probe As New Bitmap(156, 134)
            Using graphics = System.Drawing.Graphics.FromImage(probe)
                graphics.FillRectangle(Brushes.Red, 0, 0, 78, 67)
                graphics.FillRectangle(Brushes.Lime, 78, 0, 78, 67)
                graphics.FillRectangle(Brushes.Blue, 0, 67, 78, 67)
                graphics.FillRectangle(Brushes.Yellow, 78, 67, 78, 67)
            End Using
            For Each size In New Size() {New Size(640, 360), New Size(1264, 681), New Size(1920, 1080), New Size(800, 1000)}
                main.ClientSize = size
                main.Render(state)
                For Each side In New String() {"Left", "Right"}
                    For i = 1 To 3
                        Dim marker = DirectCast(FindControl(main, side & "Strike" & i & "Lbl"), PictureBox)
                        Dim original = marker.Image
                        Check(marker.SizeMode = PictureBoxSizeMode.Zoom AndAlso original.Size = New Size(156, 134), "strike uses unmodified shared image with proportional scaling")
                        Try
                            marker.Image = probe
                            Using rendered As New Bitmap(marker.Width, marker.Height)
                                marker.DrawToBitmap(rendered, New Rectangle(Point.Empty, rendered.Size))
                                Dim quadrants = {rendered.GetPixel(rendered.Width \ 4, rendered.Height \ 4), rendered.GetPixel(rendered.Width * 3 \ 4, rendered.Height \ 4), rendered.GetPixel(rendered.Width \ 4, rendered.Height * 3 \ 4), rendered.GetPixel(rendered.Width * 3 \ 4, rendered.Height * 3 \ 4)}
                                Dim expected = {Color.Red, Color.Lime, Color.Blue, Color.Yellow}
                                Check(quadrants.Select(Function(color) color.ToArgb()).SequenceEqual(expected.Select(Function(color) color.ToArgb())), "all image quadrants survive resize: " & side & i & " at " & size.ToString())
                            End Using
                        Finally
                            marker.Image = original
                        End Try
                    Next
                Next
                SaveBitmap(main, Path.Combine(directory, "Markers-" & size.Width & "x" & size.Height & ".png"))
            Next
        End Using
        main.ClientSize = New Size(1264, 681)
        main.Render(state)
        main.Hide()
    End Sub

    Private Sub TestBigTabOrder(big As BigGameControl)
        big.Activate()
        Dim processKey = GetType(BigGameControl).GetMethod("ProcessDialogKey", Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Instance)
        For i = 0 To 9
            Dim answer = FindControl(big, "BigAnswer" & i)
            Dim points = FindControl(big, "BigPoints" & i)
            answer.Focus()
            processKey.Invoke(big, New Object() {Keys.Tab})
            Check(points.ContainsFocus, "Tab moves answer to its points: " & i)
            processKey.Invoke(big, New Object() {Keys.Tab Or Keys.Shift})
            Check(answer.ContainsFocus, "Shift+Tab moves points back to answer: " & i)
            If i < 9 Then
                points.Focus()
                processKey.Invoke(big, New Object() {Keys.Tab})
                Dim nextAnswer = FindControl(big, "BigAnswer" & (i + 1))
                Check(nextAnswer.ContainsFocus, "Tab from points skips reveal buttons to next answer: " & i)
                processKey.Invoke(big, New Object() {Keys.Tab Or Keys.Shift})
                Check(points.ContainsFocus, "Shift+Tab crosses questions and players: " & i)
            End If
        Next
        FindControl(big, "BigPoints9").Focus()
        processKey.Invoke(big, New Object() {Keys.Tab})
        Check(FindControl(big, "ShowBigText0").ContainsFocus, "reveal commands remain keyboard accessible after input")
        processKey.Invoke(big, New Object() {Keys.Tab})
        Check(FindControl(big, "ShowBigText1").ContainsFocus, "Tab skips disabled reveal-points command")
    End Sub

    Private Iterator Function AllControls(parent As Control) As IEnumerable(Of Control)
        For Each control As Control In parent.Controls
            Yield control
            For Each child In AllControls(control)
                Yield child
            Next
        Next
    End Function

    Private Function FindControl(parent As Control, name As String) As Control
        For Each control As Control In parent.Controls
            If control.Name = name Then Return control
            Dim found = FindControl(control, name)
            If found IsNot Nothing Then Return found
        Next
        Return Nothing
    End Function
End Module
