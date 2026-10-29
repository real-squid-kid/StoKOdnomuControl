Option Strict On
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Reflection
Imports System.Windows.Forms
Imports System.Xml.Linq
Imports StoKOdnomuControl

Public Module AuditRegression
    Private _check As Action(Of Boolean, String)

    Public Sub Run(directory As String, check As Action(Of Boolean, String), withUi As Boolean)
        _check = check
        IO.Directory.CreateDirectory(directory)
        CheckRestart(IO.Path.Combine(directory, "restart"))
        CheckIncompleteXml(IO.Path.Combine(directory, "shape"))
        If withUi Then
            CheckProfileDraft(IO.Path.Combine(directory, "profile"))
            CheckStartupWindows(IO.Path.Combine(directory, "windows"))
        End If
        Console.WriteLine("Audit regressions: " & directory)
    End Sub

    Private Function Rejected(action As Action) As Boolean
        Try
            action()
            Return False
        Catch ex As Exception When TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is InvalidDataException OrElse TypeOf ex Is IOException
            Return True
        End Try
    End Function

    Private Function Seed(folder As String) As GameSession
        Dim game As New GameSession(folder)
        game.Execute("left 321", Sub(s) s.Main.LeftScore = 321)
        game.Execute("right 45", Sub(s) s.Main.RightScore = 45)
        Return game
    End Function

    Private Sub CheckRestart(folder As String)
        Dim original = Seed(folder)
        Dim path = IO.Path.Combine(folder, "game.xml")
        Dim primary = File.ReadAllBytes(path)
        Dim backup = File.ReadAllBytes(path & ".bak")
        Dim restarted As New GameSession(folder)
        _check(restarted.RequiresStartupChoice, "existing snapshot requires startup decision")
        Dim invoked As Boolean
        _check(Rejected(Sub() restarted.Execute("show splash", Sub(s)
                                                                   invoked = True
                                                                   GameRules.ShowSplash(s.Main.SplashScreen, "СТО", "К ОДНОМУ")
                                                               End Sub)), "first command blocked before restore")
        _check(Rejected(Sub() restarted.Execute("hide splash", Sub(s) s.Main.SplashScreen.Visible = False)), "second command cannot rotate previous match away")
        _check(Not invoked AndAlso File.ReadAllBytes(path).SequenceEqual(primary) AndAlso File.ReadAllBytes(path & ".bak").SequenceEqual(backup), "blocked commands do not execute or alter either file")
        _check(Rejected(Sub() restarted.Undo()), "undo cannot bypass startup decision")
        restarted.StartNewMatch()
        _check(Not restarted.RequiresStartupChoice AndAlso restarted.State.Main.LeftScore = 0, "explicit new match resolves startup decision")
        restarted.Execute("show", Sub(s) GameRules.ShowSplash(s.Main.SplashScreen, "СТО", "К ОДНОМУ"))
        restarted.Execute("hide", Sub(s) s.Main.SplashScreen.Visible = False)
        Dim archive = IO.Directory.GetDirectories(IO.Path.Combine(folder, "archive")).Single()
        _check(File.Exists(IO.Path.Combine(archive, "COMPLETE.txt")) AndAlso File.ReadAllBytes(IO.Path.Combine(archive, "game.xml")).SequenceEqual(primary) AndAlso File.ReadAllBytes(IO.Path.Combine(archive, "game.xml.bak")).SequenceEqual(backup), "new match preserves both previous snapshots outside rotating backup")
        Dim archived As New GameSession(archive)
        archived.Restore()
        _check(archived.State.Main.LeftScore = 321 AndAlso archived.State.Main.RightScore = 45 AndAlso archived.UndoDescription = "right 45", "archived match remains restorable with history")
        Dim live = Seed(IO.Path.Combine(folder, "live-new"))
        live.StartNewMatch()
        live.Undo()
        _check(live.State.Main.LeftScore = 321, "explicit new match remains undoable in a running session")
        Dim backupFolder = IO.Path.Combine(folder, "backup-only")
        Seed(backupFolder)
        File.Delete(IO.Path.Combine(backupFolder, "game.xml"))
        Dim onlyBackup As New GameSession(backupFolder)
        _check(onlyBackup.RequiresStartupChoice AndAlso Rejected(Sub() onlyBackup.Execute("blocked", Sub(s) s.Main.Bank = 99)), "backup alone also protects startup")
        onlyBackup.Restore(True)
        onlyBackup.Execute("continued", Sub(s) s.Main.Bank = 99)
        _check(onlyBackup.State.Main.LeftScore = 321, "backup can continue when primary absent")
        Dim failedFolder = IO.Path.Combine(folder, "archive-failure")
        Seed(failedFolder)
        Dim before = File.ReadAllBytes(IO.Path.Combine(failedFolder, "game.xml"))
        File.WriteAllText(IO.Path.Combine(failedFolder, "archive"), "blocked directory")
        Dim failed As New GameSession(failedFolder)
        _check(Rejected(Sub() failed.StartNewMatch()) AndAlso failed.RequiresStartupChoice AndAlso File.ReadAllBytes(IO.Path.Combine(failedFolder, "game.xml")).SequenceEqual(before), "failed archive prevents new-match publication")
        Dim lockedFolder = IO.Path.Combine(folder, "archive-lock")
        Seed(lockedFolder)
        Dim lockedSession As New GameSession(lockedFolder)
        Using locked As New FileStream(IO.Path.Combine(lockedFolder, "game.xml.bak"), FileMode.Open, FileAccess.Read, FileShare.None)
            _check(Rejected(Sub() lockedSession.StartNewMatch()) AndAlso lockedSession.RequiresStartupChoice AndAlso IO.Directory.GetFiles(IO.Path.Combine(lockedFolder, "archive"), "COMPLETE.txt", SearchOption.AllDirectories).Length = 0, "partial archive failure retains startup protection")
        End Using
        Dim first = IO.Path.Combine(folder, "relative-a")
        Dim second = IO.Path.Combine(folder, "relative-b")
        IO.Directory.CreateDirectory(first)
        IO.Directory.CreateDirectory(second)
        Seed(IO.Path.Combine(first, "data"))
        Dim previousDirectory = Environment.CurrentDirectory
        Try
            Environment.CurrentDirectory = first
            Dim relative As New GameSession("data")
            Environment.CurrentDirectory = second
            _check(relative.RequiresStartupChoice AndAlso Rejected(Sub() relative.Execute("wrong cwd", Sub(s) s.Main.Bank = 1)), "working-directory change cannot bypass startup protection")
            relative.Restore()
            _check(relative.State.Main.LeftScore = 321, "snapshot location is bound at session construction")
        Finally
            Environment.CurrentDirectory = previousDirectory
        End Try
        Console.WriteLine("P1: restart, backup-only, archived new match and archive failure PASS")
    End Sub

    Private Sub CheckIncompleteXml(folder As String)
        Dim game As New GameSession(folder)
        game.Execute("keep 654", Sub(s) s.Main.LeftScore = 654)
        Dim path = IO.Path.Combine(folder, "game.xml")
        Dim valid = XDocument.Load(path)
        Dim bad As New List(Of XDocument) From {XDocument.Parse("<SavedMatch />"), XDocument.Parse("<SavedMatch><Current/><History/></SavedMatch>")}
        For Each selector In New String() {"Current", "History", "Current/FormatVersion", "Current/Main", "Current/Big", "Current/Main/LeftScore", "Current/Main/RightScore", "Current/Main/Bank", "Current/Main/BankId", "Current/Main/Round", "Current/Main/Answers", "Current/Main/LeftStrikes", "Current/Main/RightStrikes", "Current/Main/SplashScreen", "Current/Main/SplashScreen/Line1", "Current/Big/Answers", "Current/Big/TargetScore", "Current/Big/HideFirstPlayer", "Current/Big/ScoreAdjustment", "History/UndoEntry/Description", "History/UndoEntry/State", "History/UndoEntry/State/FormatVersion", "History/UndoEntry/State/Main", "History/UndoEntry/State/Big"}
            Dim candidate As New XDocument(valid)
            At(candidate.Root, selector).Remove()
            bad.Add(candidate)
        Next
        Dim missingAnswer As New XDocument(valid)
        missingAnswer.Root.Element("Current").Element("Main").Element("Answers").Elements().First().Element("TextVisible").Remove()
        bad.Add(missingAnswer)
        Dim duplicate As New XDocument(valid)
        duplicate.Root.Add(New XElement(duplicate.Root.Element("Current")))
        bad.Add(duplicate)
        For Each selector As String In {"", "History/UndoEntry"}
            Dim nilDocument As New XDocument(valid)
            Dim nilElement = If(selector = "", nilDocument.Root, At(nilDocument.Root, selector))
            nilElement.SetAttributeValue(XName.Get("nil", "http://www.w3.org/2001/XMLSchema-instance"), "true")
            bad.Add(nilDocument)
        Next
        For i = 0 To bad.Count - 1
            bad(i).Save(path)
            _check(Rejected(Sub() game.Restore()) AndAlso game.State.Main.LeftScore = 654 AndAlso game.UndoDescription = "keep 654", "incomplete XML preserves active state and undo " & i)
        Next
        File.WriteAllText(path & ".bak", "<SavedMatch />")
        _check(Rejected(Sub() game.Restore(True)) AndAlso game.State.Main.LeftScore = 654, "incomplete backup also rejected")
        Dim legacy As New XDocument(valid)
        For Each snapshot In New XElement() {legacy.Root.Element("Current"), legacy.Root.Element("History").Element("UndoEntry").Element("State")}
            snapshot.Element("FormatVersion").Value = "1"
            snapshot.Element("Main").Element("SplashScreen").Remove()
            snapshot.Element("Main").Add(New XElement("Splash", "-1"))
        Next
        legacy.Save(path)
        game.Restore()
        _check(game.State.FormatVersion = 2 AndAlso game.State.Main.LeftScore = 654, "complete format 1 remains supported")
        game.Undo()
        _check(game.State.Main.LeftScore = 0, "migrated history still works")
        Console.WriteLine("P2: incomplete current/history XML and legacy migration PASS")
    End Sub

    Private Function At(root As XElement, path As String) As XElement
        Dim current = root
        For Each name In path.Split("/"c)
            current = current.Element(name)
        Next
        Return current
    End Function

    Private Function Find(parent As Control, name As String) As Control
        Return parent.Controls.Find(name, True).Single()
    End Function

    ' Ограниченное ожидание конкретного окна; ошибки callback возвращаются тесту,
    ' не оставляя открытой модалки и не вызывая JIT-диалог.
    Private Sub AnswerDialog(name As String, answer As Action(Of Form), action As Action)
        Dim answered As Boolean
        Dim failure As Exception = Nothing
        Dim watch = Stopwatch.StartNew()
        Using timer As New System.Windows.Forms.Timer With {.Interval = 20}
            AddHandler timer.Tick, Sub()
                                       Dim dialog = Application.OpenForms.Cast(Of Form)().FirstOrDefault(Function(f) f.Name = name AndAlso f.Visible)
                                       If dialog Is Nothing Then
                                           If watch.ElapsedMilliseconds < 5000 Then Return
                                           timer.Stop()
                                           failure = New Exception("Dialog timeout: " & name)
                                           For Each other In Application.OpenForms.Cast(Of Form)().Where(Function(f) f.Modal).ToArray()
                                               other.DialogResult = DialogResult.Cancel
                                           Next
                                           Return
                                       End If
                                       timer.Stop()
                                       dialog.Opacity = 0
                                       answered = True
                                       Try
                                           answer(dialog)
                                       Catch ex As Exception
                                           failure = ex
                                           dialog.DialogResult = DialogResult.Cancel
                                       End Try
                                   End Sub
            timer.Start()
            action()
            While Not answered AndAlso failure Is Nothing AndAlso watch.ElapsedMilliseconds < 5000
                Application.DoEvents()
                Threading.Thread.Sleep(1)
            End While
        End Using
        If failure IsNot Nothing Then Throw failure
        _check(answered, "expected dialog answered: " & name)
    End Sub

    Private Sub CheckProfileDraft(folder As String)
        Using service As New AppearanceService(folder), settings As New AppearanceSettings(service)
            settings.Opacity = 0
            settings.ShowInTaskbar = False
            settings.Show()
            Dim name = Find(settings, "ProfileName")
            Dim list = DirectCast(Find(settings, "ProfileList"), ComboBox)
            name.Text = "UNSAVED CUSTOM PROFILE"
            settings.SetTextColor(Color.Coral)
            Dim index = Enumerable.Range(0, list.Items.Count).Single(Function(i) list.Items(i).ToString() = "По умолчанию")
            AnswerDialog("UnsavedChanges", Sub(dialog) DirectCast(Find(dialog, "ReviewChanges"), Button).PerformClick(), Sub() list.SelectedIndex = index)
            _check(list.SelectedIndex = 0 AndAlso name.Text = "UNSAVED CUSTOM PROFILE" AndAlso settings.Dirty AndAlso Find(settings, "AppearanceColor").BackColor.ToArgb() = Color.Coral.ToArgb(), "cancelled selection preserves whole draft and previous index")
            Dim broken = IO.Path.Combine(folder, "absent.xml")
            Dim badIndex = list.Items.Add(New ProfileChoice With {.Name = "Broken", .ConfigFile = broken})
            list.SelectedIndex = badIndex
            _check(list.SelectedIndex = 0 AndAlso name.Text = "UNSAVED CUSTOM PROFILE" AndAlso settings.Dirty AndAlso Find(settings, "SettingsError").Text <> "", "load failure preserves draft and restores index without discard prompt")
            AnswerDialog("UnsavedChanges", Sub(dialog) DirectCast(Find(dialog, "DiscardChanges"), Button).PerformClick(), Sub() list.SelectedIndex = index)
            _check(name.Text = "По умолчанию" AndAlso list.SelectedIndex = index AndAlso settings.Dirty, "confirmed profile switch replaces draft")
            DirectCast(Find(settings, "ApplySettings"), Button).PerformClick()
            _check(Not settings.Dirty AndAlso list.SelectedIndex = 0 AndAlso service.Current.Profile.Name = "По умолчанию", "apply resets selection without a second confirmation")
            settings.Close()
        End Using
        Console.WriteLine("P3: discard refusal, agreement, bad profile and apply PASS")
    End Sub

    Private Function Session(main As Form1) As GameSession
        Return DirectCast(GetType(Form1).GetField("_session", BindingFlags.Instance Or BindingFlags.NonPublic).GetValue(main), GameSession)
    End Function

    Private Sub CheckStartupWindows(folder As String)
        Dim previous = Environment.GetEnvironmentVariable("STOKODNOMU_DATA_DIR")
        Try
            Dim startup = IO.Path.Combine(folder, "choice")
            Seed(startup)
            Environment.SetEnvironmentVariable("STOKODNOMU_DATA_DIR", startup)
            Using main As New Form1(New BuzzManager(Function() Nothing))
                main.Opacity = 0
                main.ShowInTaskbar = False
                AnswerDialog("StartupMatchChoice", Sub(dialog)
                                                      _check(DirectCast(dialog.AcceptButton, Button).Name = "StartupCancel", "startup defaults to cancellation")
                                                      Save(dialog, IO.Path.Combine(folder, "startup-choice.png"))
                                                      DirectCast(Find(dialog, "StartupCancel"), Button).PerformClick()
                                                  End Sub, Sub() main.Show())
                _check(Session(main).RequiresStartupChoice AndAlso Not Find(main, "ToggleMainAnswer0").Enabled, "cancel keeps game commands blocked")
                AnswerDialog("StartupMatchChoice", Sub(dialog) DirectCast(Find(dialog, "StartupRestore"), Button).PerformClick(),
                             Sub() GetType(Form1).GetMethod("ResolveStartupChoice", BindingFlags.Instance Or BindingFlags.NonPublic).Invoke(main, Nothing))
                _check(Not Session(main).RequiresStartupChoice AndAlso Session(main).State.Main.LeftScore = 321 AndAlso Find(main, "ToggleMainAnswer0").Enabled, "startup restore enables original match")
            End Using
            Dim fresh = IO.Path.Combine(folder, "new")
            Seed(fresh)
            Environment.SetEnvironmentVariable("STOKODNOMU_DATA_DIR", fresh)
            Using main As New Form1(New BuzzManager(Function() Nothing))
                main.Opacity = 0
                main.ShowInTaskbar = False
                AnswerDialog("StartupMatchChoice", Sub(dialog) DirectCast(Find(dialog, "StartupNewMatch"), Button).PerformClick(), Sub() main.Show())
                _check(Not Session(main).RequiresStartupChoice AndAlso Session(main).State.Main.LeftScore = 0 AndAlso IO.Directory.GetDirectories(IO.Path.Combine(fresh, "archive")).Length = 1, "startup new match archives old files before enabling commands")
            End Using
            Dim damaged = IO.Path.Combine(folder, "damaged")
            Seed(damaged)
            File.WriteAllText(IO.Path.Combine(damaged, "game.xml"), "<broken>")
            Environment.SetEnvironmentVariable("STOKODNOMU_DATA_DIR", damaged)
            Using main As New Form1(New BuzzManager(Function() Nothing))
                main.Opacity = 0
                main.ShowInTaskbar = False
                AnswerDialog("StartupMatchChoice", Sub(dialog)
                                                      DirectCast(Find(dialog, "StartupRestore"), Button).PerformClick()
                                                      _check(Session(main).RequiresStartupChoice AndAlso Find(dialog, "StartupMatchError").Text <> "", "failed startup restore keeps choice open and match protected")
                                                      DirectCast(Find(dialog, "StartupSaveSource"), ComboBox).SelectedIndex = 1
                                                      DirectCast(Find(dialog, "StartupRestore"), Button).PerformClick()
                                                  End Sub, Sub() main.Show())
                _check(Not Session(main).RequiresStartupChoice AndAlso Session(main).State.Main.LeftScore = 321, "startup can recover backup after corrupt primary")
            End Using
            Dim broken = IO.Path.Combine(folder, "error")
            IO.Directory.CreateDirectory(IO.Path.Combine(broken, "appearance"))
            File.WriteAllText(IO.Path.Combine(broken, "appearance", "config.xml"), "<broken>")
            Environment.SetEnvironmentVariable("STOKODNOMU_DATA_DIR", broken)
            Using main As New Form1(New BuzzManager(Function() Nothing))
                _check(Not main.IsHandleCreated AndAlso Find(main, "OperatorError").Text <> "", "startup warning is populated before handle creation")
                main.Opacity = 0
                main.ShowInTaskbar = False
                main.Show()
                Application.DoEvents()
                _check(Find(main, "OperatorError").Visible AndAlso Find(main, "OperatorError").Text.Contains("Не удалось загрузить оформление"), "startup warning remains visible after Show")
                Save(main, IO.Path.Combine(folder, "startup-profile-error.png"))
            End Using
        Finally
            Environment.SetEnvironmentVariable("STOKODNOMU_DATA_DIR", previous)
        End Try
        Console.WriteLine("P4 and startup UI: warning, cancel, restore and new match PASS")
    End Sub

    Private Sub Save(form As Form, path As String)
        IO.Directory.CreateDirectory(IO.Path.GetDirectoryName(path))
        Using image As New Bitmap(form.Width, form.Height)
            form.DrawToBitmap(image, New Rectangle(Point.Empty, image.Size))
            image.Save(path)
        End Using
    End Sub
End Module
