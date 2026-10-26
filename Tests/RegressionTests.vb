Option Strict On
Imports Microsoft.VisualBasic
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
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
            TestBuzzer()
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
        game.Execute("splash", Sub(s) s.Main.Splash = 2)
        Check(game.State.Main.Answers(0).TextVisible AndAlso game.State.Main.Bank = 20, "splash preserves game")
        game.Execute("return", Sub(s) s.Main.Splash = -1)
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

    Private Sub TestPersistence(directory As String)
        ' История должна переживать перезапуск, а повреждённый XML — не заменять
        ' открытую игру. File.Replace требует обычных прав ОС без запрета песочницы.
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
        Using main As New Form1(), countdown As New Countdown()
            Dim game = Session(directory, "ui-big")
            Using big As New BigGameControl(game, countdown, Sub() Return, Sub() game.Undo()), board As New Tablo(), bigBoard As New BigGameTablo()
                For Each form As Form In New Form() {main, big, board, bigBoard}
                    ' Show создаёт layout/контролы; непоказанная форма давала пустой DrawToBitmap.
                    ' Прозрачность скрывает тестовые окна от пользователя.
                    form.ShowInTaskbar = False
                    form.Opacity = 0
                    form.Show()
                    Application.DoEvents()
                    If form Is main Then
                        DirectCast(FindControl(main, "MainAnswer0"), TextBox).Text = "Проверочный ответ"
                        DirectCast(FindControl(main, "MainPoints0"), NumericUpDown).Value = 10
                        DirectCast(FindControl(main, "ToggleMainAnswer0"), Button).PerformClick()
                        Check(DirectCast(FindControl(main, "RoundBank"), NumericUpDown).Value = 10, "main operator reveal updates bank")
                        DirectCast(FindControl(main, "ToggleMainAnswer0"), Button).PerformClick()
                        DirectCast(FindControl(main, "ToggleMainAnswer0"), Button).PerformClick()
                        Check(DirectCast(FindControl(main, "RoundBank"), NumericUpDown).Value = 10, "main operator hide/reopen preserves bank")
                        DirectCast(FindControl(main, "MainPoints0"), NumericUpDown).Value = 15
                        DirectCast(FindControl(main, "EditMainAnswer0"), Button).PerformClick()
                        Check(DirectCast(FindControl(main, "RoundBank"), NumericUpDown).Value = 15, "main operator applies correction")
                        DirectCast(FindControl(main, "UndoLastAction"), Button).PerformClick()
                        Check(DirectCast(FindControl(main, "RoundBank"), NumericUpDown).Value = 10, "main operator undo works")
                    ElseIf form Is big Then
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
            End Using
        End Using
        Console.WriteLine("UI snapshots: " & directory)
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
