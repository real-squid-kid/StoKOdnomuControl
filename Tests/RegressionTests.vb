Option Strict On
Imports Microsoft.VisualBasic
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports System.Collections.Concurrent
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Runtime.InteropServices
Imports StoKOdnomuControl

Module Checks
    ' Консольные регрессии без дополнительного тестового фреймворка/NuGet.
    ' Счётчик ниже — число утверждений, а не независимых пользовательских сценариев.
    Private _assertions As Integer
    Private ReadOnly _storageErrors As New List(Of String)()

    <STAThread>
    Public Sub Main(args As String())
        Try
            If args.Length > 0 AndAlso args(0) = "--package-smoke" Then
                PackageChecks.Run(args(1))
                Return
            End If
            If args.Length > 0 AndAlso args(0) = "--interrupt-write" Then
                StabilityChecks.InterruptedWriter(args(1), args(2))
                Return
            End If
            ' Ошибки UI-тестов доходят до консольного отчёта, а не до JIT-диалога
            ' на рабочем столе владельца. Это политика только тестового процесса.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException)
            ' Снимки/логи тестов отделены от сохранения реального матча оператора.
            Dim temporary = Path.Combine(Path.GetTempPath(), "StoKOdnomu-tests-" & Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(temporary)
            If args.Contains("--native-dialog") Then
                Application.EnableVisualStyles()
                Using owner As New Form With {.Text = "Native dialog test", .Opacity = 0, .ShowInTaskbar = False}
                    owner.Show()
                    TestNativePrimary(owner)
                End Using
                Console.WriteLine("PASS: native dialog")
                Return
            End If
            If args.Contains("--audit-fixes") Then
                Application.EnableVisualStyles()
                AuditRegression.Run(temporary, AddressOf Check, True)
                Console.WriteLine("PASS: " & _assertions & " audit assertions")
                Return
            End If
            If args.Contains("--big-input") Then
                Application.EnableVisualStyles()
                AudioService.Instance.Enabled = False
                Dim game = Session(temporary, "big-input")
                Using timer As New Countdown(), big As New BigGameControl(game, timer, Sub() Return, Sub() game.Undo(), Sub() Return, Sub() Return, Sub() Return)
                    big.ShowInTaskbar = False
                    big.Show()
                    TestBigTabOrder(big)
                    FindControl(big, "BigAnswer0").Focus()
                    GetType(BigGameControl).GetMethod("ProcessDialogKey", Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Instance).Invoke(big, New Object() {Keys.Tab})
                    SaveBitmap(big, Path.Combine(temporary, "big-game-input.png"))
                End Using
                Console.WriteLine("Input preview: " & temporary)
                Console.WriteLine("PASS: " & _assertions & " big-input assertions")
                Return
            End If
            If args.Contains("--scale-warning") Then
                Application.EnableVisualStyles()
                DisplayScaleChecks.Run(temporary, AddressOf Check, True)
                Console.WriteLine("PASS: " & _assertions & " display-scale assertions")
                Return
            End If
            If args.Contains("--usb-io") Then
                UsbIoChecks.Run(AddressOf Check)
                Console.WriteLine("PASS: " & _assertions & " USB I/O assertions")
                Return
            End If
            TestParser()
            TestBankDocuments(temporary)
            TestAppearanceProfiles(temporary)
            TestMainGame(temporary)
            TestBigGame(temporary)
            TestSplashes(temporary)
            TestLegacySplashes(temporary)
            TestRetiredBigSplash(temporary)
            TestBuzzer()
            TestBuzzConnection()
            UsbIoChecks.Run(AddressOf Check)
            TestPersistence(temporary)
            TestCountdown()
            DisplayScaleChecks.Run(Path.Combine(temporary, "display-scale"), AddressOf Check, args.Contains("--ui"))
            AuditRegression.Run(Path.Combine(temporary, "audit"), AddressOf Check, args.Contains("--ui"))
            If args.Contains("--stability") Then StabilityChecks.Run(Path.Combine(temporary, "stability"), AddressOf Check)
            If args.Contains("--ui") Then TestUi(temporary)
            If args.Contains("--stability") AndAlso args.Contains("--ui") Then PackageChecks.SoakUi(temporary, AddressOf Check)
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

    Private Sub TestBankDocuments(directory As String)
        Dim folder = Path.Combine(directory, "banks")
        IO.Directory.CreateDirectory(folder)
        Dim shipped = IO.Path.GetFullPath(IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../StoKOdnomuControl"))
        For Each source In StabilityChecks.BankNames.Select(Function(name) IO.Path.Combine(shipped, name))
            Dim document = BankDocument.Open(source)
            Dim before = BankDocument.Serialize(document.Bank)
            Dim target = Path.Combine(folder, Path.GetFileName(source))
            document.Save(target, document.Bank)
            Check(BankDocument.Serialize(BankDocument.Open(target).Bank) = before, "bank roundtrip " & Path.GetFileName(source))
            Dim bytes = File.ReadAllBytes(target)
            Check(Not bytes.Take(3).SequenceEqual(New Byte() {239, 187, 191}) AndAlso File.ReadAllText(target).Contains(vbCrLf), "bank writes UTF8 no BOM and CRLF")
            document.Bank.Rounds(0)(0).Text = "НОВЫЙ ОТВЕТ"
            document.Save(target, document.Bank)
            Check(File.Exists(target & ".bak") AndAlso BankDocument.Serialize(BankDocument.Open(target & ".bak").Bank) = before, "bank backup retains previous valid data")
            File.AppendAllText(target, vbCrLf)
            Dim changed = File.ReadAllBytes(target)
            Dim conflict As Boolean
            Try
                document.Save(target, document.Bank)
            Catch ex As BankConflictException
                conflict = True
            End Try
            Check(conflict AndAlso changed.SequenceEqual(File.ReadAllBytes(target)), "external bank edit is never overwritten")
            document.Save(target & ".copy.txt", document.Bank)
            Check(document.FilePath.EndsWith(".copy.txt"), "conflicted draft can Save As")
        Next
        Dim valid = BankDocument.Open(IO.Path.Combine(shipped, "base.txt")).Bank
        valid.Rounds(0)(0).Text = "Нельзя&так"
        Reject(Sub() BankDocument.Serialize(valid), "writer rejects ambiguous delimiter")
        valid.Rounds(0)(0).Text = "Две" & vbLf & "строки"
        Reject(Sub() BankDocument.Serialize(valid), "writer rejects embedded newline")
        valid.Rounds(0)(0).Text = ""
        Reject(Sub() BankDocument.Serialize(valid), "writer rejects blank required answer")
        valid.Rounds(0)(0).Text = New String("я"c, 121)
        Reject(Sub() BankDocument.Serialize(valid), "writer rejects overlong answer")
        valid.Rounds(0)(0).Text = "Текст"
        valid.Rounds(0)(0).Points = 1000
        Reject(Sub() BankDocument.Serialize(valid), "writer rejects excessive people count")
        valid.Rounds(0)(0).Points = 10
        valid.Rounds(3)(0).Points = 99
        Reject(Sub() BankDocument.Serialize(valid), "reverse prices cannot be edited in TXT writer")
        valid.Rounds(3)(0).Points = 15
        Dim blocked = Path.Combine(folder, "blocked.txt")
        IO.Directory.CreateDirectory(blocked)
        Dim unsaved As New BankDocument()
        Dim failed As Boolean
        Try
            unsaved.Save(blocked, valid)
        Catch ex As IOException
            failed = True
        End Try
        Check(failed AndAlso unsaved.FilePath = "" AndAlso Not File.Exists(blocked), "failed bank save does not publish document path")
        Dim bom = Path.Combine(folder, "bom.txt")
        File.WriteAllText(bom, BankDocument.Serialize(valid).Replace(vbCrLf, vbLf), New System.Text.UTF8Encoding(True))
        Check(BankDocument.Open(bom).Bank.Rounds(0)(0).Text = "Текст", "document opens BOM and LF")
    End Sub

    Private Sub TestAppearanceProfiles(directory As String)
        Dim folder = Path.Combine(directory, "appearance")
        IO.Directory.CreateDirectory(folder)
        Dim image = Path.Combine(folder, "source.png")
        Using bitmap As New Bitmap(240, 140)
            Using graphics = System.Drawing.Graphics.FromImage(bitmap)
                graphics.Clear(Color.Navy)
                graphics.FillRectangle(Brushes.Gold, 0, 0, 120, 70)
            End Using
            bitmap.Save(image, Imaging.ImageFormat.Png)
        End Using
        Dim defaults As New AppearanceProfile()
        Check(defaults.BuzzLampDelayMilliseconds = 250, "default Buzz lamp delay is 250ms")
        Dim oldXml As New System.Xml.Linq.XDocument()
        Using stream As New MemoryStream()
            Dim profileSerializer As New System.Xml.Serialization.XmlSerializer(GetType(AppearanceProfile))
            profileSerializer.Serialize(stream, defaults)
            stream.Position = 0
            oldXml = System.Xml.Linq.XDocument.Load(stream)
        End Using
        oldXml.Root.Element("BuzzLampDelayMilliseconds").Remove()
        Dim oldProfile = Path.Combine(folder, "legacy-config.xml")
        oldXml.Save(oldProfile)
        Check(AppearanceStorage.Open(oldProfile).BuzzLampDelayMilliseconds = 250, "old profile without delay keeps default")
        defaults.BuzzLampDelayMilliseconds = -1
        Reject(Sub() AppearanceStorage.Validate(defaults), "negative lamp delay rejected")
        defaults.BuzzLampDelayMilliseconds = 5001
        Reject(Sub() AppearanceStorage.Validate(defaults), "excessive lamp delay rejected")
        Dim profile As New AppearanceProfile With {.Name = "Тест мероприятия", .SoundEnabled = False, .BuzzLampDelayMilliseconds = 750}
        profile.Main.Background = image
        profile.Main.LeftStrike = image
        profile.Main.LeftClear = image
        profile.Main.RightStrike = image
        profile.Main.RightClear = image
        profile.Main.TextArgb = Color.White.ToArgb()
        profile.Big.Background = image
        profile.Big.TextArgb = Color.Gold.ToArgb()
        profile.Rounds(1).Inherit = False
        profile.Rounds(1).TextArgb = Color.Crimson.ToArgb()
        Check(profile.MainStyle(1).TextArgb = Color.White.ToArgb() AndAlso profile.MainStyle(2).TextArgb = Color.Crimson.ToArgb(), "round styles inherit with explicit override")
        Dim storage = Path.Combine(folder, "installed")
        Using service As New AppearanceService(storage)
            Dim changes As Integer
            AddHandler service.Changed, Sub() changes += 1
            service.Apply(profile)
            Check(changes = 1 AndAlso service.Current.Profile.Name = profile.Name, "profile apply publishes once after storage")
            Check(service.Current.Profile.BuzzLampDelayMilliseconds = 750 AndAlso AppearanceStorage.Open(Path.Combine(storage, "config.xml")).BuzzLampDelayMilliseconds = 750, "lamp delay cloned and persisted")
            Check(Not service.Current.Profile.Main.Background.Equals(image), "profile copies media into owned storage")
            Dim snapshot = service.Current.Profile
            snapshot.Name = "посторонняя правка"
            Check(service.Current.Profile.Name = profile.Name, "profile consumers cannot mutate active model")
            Using exclusive As New FileStream(image, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                Check(exclusive.Length > 0, "decoded image never locks source")
            End Using
            Dim broken = service.Current.Profile
            broken.Main.Background = Path.Combine(folder, "absent.png")
            Reject(Sub() service.Apply(broken), "missing media rejects entire apply")
            Check(changes = 1 AndAlso service.Current.Profile.Name = profile.Name, "failed apply leaves active profile")
            Dim portable = Path.Combine(folder, "portable")
            Using exported = AppearanceStorage.Save(service.Current.Profile, portable)
            End Using
            Dim moved = Path.Combine(folder, "moved")
            IO.Directory.Move(portable, moved)
            Dim opened = AppearanceStorage.Open(Path.Combine(moved, "config.xml"))
            Using decoded As New LoadedAppearance(opened)
                Check(decoded.Picture(opened.Main.Background, Nothing).Width = 240, "portable folder opens after move")
                Check(opened.Big.TextArgb = Color.Gold.ToArgb(), "big style remains independent")
            End Using
            Dim updated = service.Current.Profile
            updated.Name = "Второй профиль"
            Dim mediaCount = IO.Directory.GetFiles(Path.Combine(storage, "media"), "*.png", SearchOption.AllDirectories).Length
            service.Apply(updated)
            Check(IO.Directory.GetFiles(Path.Combine(storage, "media"), "*.png", SearchOption.AllDirectories).Length = mediaCount, "reapplying profile does not duplicate installed images")
            Check(File.Exists(Path.Combine(storage, "config.xml.bak")), "profile has backup manifest")
            Dim previous = AppearanceStorage.Open(Path.Combine(storage, "config.xml.bak"))
            Check(previous.Name = profile.Name AndAlso File.Exists(previous.Main.Background), "old media remain usable by backup")
            Using restarted As New AppearanceService(storage)
                Check(restarted.StartupError = "" AndAlso restarted.Current.Profile.Name = updated.Name, "selected profile survives restart")
            End Using
            Dim blocked = Path.Combine(folder, "blocked")
            IO.Directory.CreateDirectory(Path.Combine(blocked, "config.xml"))
            Using denied As New AppearanceService(blocked)
                Dim failed As Boolean
                Try
                    denied.Apply(updated)
                Catch ex As IOException
                    failed = True
                End Try
                Check(failed AndAlso denied.Current.Profile.Name = "Тупикон 2026", "profile disk failure keeps active defaults")
            End Using
            Dim xml = File.ReadAllText(Path.Combine(storage, "config.xml"))
            Dim parsed = System.Xml.Linq.XDocument.Parse(xml)
            parsed.Root.Element("Main").Element("Background").Value = "../source.png"
            Dim unsafePath = Path.Combine(storage, "unsafe.xml")
            parsed.Save(unsafePath)
            Reject(Sub() AppearanceStorage.Open(unsafePath), "portable profile cannot escape folder")
            parsed.Root.Element("Main").Element("Background").Value = image
            parsed.Save(unsafePath)
            Reject(Sub() AppearanceStorage.Open(unsafePath), "portable profile rejects absolute media path")
            parsed.Root.Element("Version").Value = "999"
            parsed.Save(unsafePath)
            Reject(Sub() AppearanceStorage.Open(unsafePath), "future profile version rejected")
        End Using
        File.WriteAllText(Path.Combine(storage, "config.xml"), "<broken>")
        Using recovered As New AppearanceService(storage)
            Check(recovered.StartupError <> "" AndAlso recovered.Current.Profile.Main.Background = "", "bad startup profile uses built-in fallback with error")
        End Using
        Dim corrupt = Path.Combine(folder, "bad.png")
        File.WriteAllText(corrupt, "not an image")
        profile.Main.Background = corrupt
        Dim badImage As Boolean
        Try
            Using loaded As New LoadedAppearance(profile)
            End Using
        Catch ex As Exception
            badImage = True
        End Try
        Check(badImage, "corrupt image rejected before publication")
        Dim legacyFolder = Path.Combine(folder, "legacy")
        IO.Directory.CreateDirectory(legacyFolder)
        File.WriteAllText(Path.Combine(legacyFolder, "profile.xml"), "<AppearanceProfile><Name>Встроенное оформление</Name><Main><TextArgb>-1</TextArgb></Main></AppearanceProfile>")
        Using legacy As New AppearanceService(legacyFolder)
            Check(legacy.StartupError = "" AndAlso legacy.Current.Profile.Name = "Тупикон 2026" AndAlso legacy.Current.Profile.Main.TextArgb = Color.White.ToArgb(), "legacy filename and decimal color stay compatible")
        End Using
        Console.WriteLine("Preparation storage: " & folder)
    End Sub

    Private Function FieldValue(Of T)(owner As Object, name As String) As T
        Return DirectCast(owner.GetType().GetField(name, Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).GetValue(owner), T)
    End Function

    Private Function StateXml(state As MatchState) As String
        Using writer As New StringWriter(Globalization.CultureInfo.InvariantCulture)
            Dim serializer As New Xml.Serialization.XmlSerializer(GetType(MatchState))
            serializer.Serialize(writer, state)
            Return writer.ToString()
        End Using
    End Function

    Private Sub TestRootPendingCancel(main As Form1)
        Dim stage As Integer
        Dim started = Diagnostics.Stopwatch.StartNew()
        Using driver As New System.Windows.Forms.Timer With {.Interval = 20}
            AddHandler driver.Tick, Sub()
                                        If started.ElapsedMilliseconds > 5000 Then Throw New Exception("Timed out waiting for exit/pending dialog at stage " & stage)
                                        Dim expected = If(stage = 0, "ExitConfirmation", "UnsavedChanges")
                                        Dim dialog = Application.OpenForms.Cast(Of Form)().SingleOrDefault(Function(f) f.Name = expected AndAlso f.Visible)
                                        If dialog Is Nothing Then Return
                                        dialog.Opacity = 0
                                        If stage = 0 Then
                                            stage = 1
                                            DirectCast(FindControl(dialog, "ConfirmExitYes"), Button).PerformClick()
                                        Else
                                            driver.Stop()
                                            stage = 2
                                            DirectCast(FindControl(dialog, "ReviewChanges"), Button).PerformClick()
                                        End If
                                    End Sub
            driver.Start()
            main.Close()
        End Using
        Check(stage = 2, "application exit consults settings draft before stopping services")
    End Sub

    Private Sub PendingAnswer(owner As Form, button As String, action As Action)
        Dim answered As Boolean
        Dim started = Diagnostics.Stopwatch.StartNew()
        Using driver As New System.Windows.Forms.Timer With {.Interval = 20}
            AddHandler driver.Tick, Sub()
                                        If started.ElapsedMilliseconds > 5000 Then Throw New Exception("Timed out waiting for pending-changes dialog")
                                        Dim dialog = Application.OpenForms.Cast(Of Form)().SingleOrDefault(Function(f) f.Name = "UnsavedChanges" AndAlso f.Visible)
                                        If dialog Is Nothing Then Return
                                        driver.Stop()
                                        dialog.Opacity = 0
                                        Check(DirectCast(dialog.AcceptButton, Button).Name = "ReviewChanges", "pending dialog defaults to retaining draft")
                                        Check(dialog.Text = "Есть несохраненные изменения" AndAlso AllControls(dialog).OfType(Of Button)().Count() = 2, "discard dialog has exact title and two choices")
                                        Check(Screen.FromControl(dialog).Primary, "discard dialog opens on primary screen")
                                        SaveBitmap(dialog, Path.Combine(Path.GetTempPath(), "StoKOdnomu-unsaved-preview.png"))
                                        DirectCast(FindControl(dialog, button), Button).PerformClick()
                                        answered = True
                                    End Sub
            driver.Start()
            action()
        End Using
        Check(answered, "pending change dialog handled " & button)
    End Sub

    Private Sub TestPreparationUi(main As Form1, directory As String)
        main.Show()
        DirectCast(FindControl(main, "OpenBankEditor"), Button).PerformClick()
        Dim editor = main.OwnedForms.OfType(Of BankEditor)().Single()
        editor.Opacity = 0
        editor.ShowInTaskbar = False
        DirectCast(FindControl(main, "OpenBankEditor"), Button).PerformClick()
        Check(main.OwnedForms.OfType(Of BankEditor)().Count() = 1, "editor button reuses window")
        Dim shipped = IO.Path.GetFullPath(IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../StoKOdnomuControl"))
        editor.OpenDocument(IO.Path.Combine(shipped, "base.txt"))
        Dim game = FieldValue(Of GameSession)(main, "_session")
        game.Execute("prepare big game", Sub(state)
                                            GameRules.ShowBigText(state.Big, 0, "СЕМЬЯ")
                                            GameRules.ShowBigPoints(state.Big, 0, 50)
                                            state.Big.HideFirstPlayer = True
                                        End Sub)
        Dim before = GameRules.Clone(game.State)
        Dim originalAnswer = FindControl(editor, "BankAnswer0_0").Text
        FindControl(editor, "BankAnswer0_0").Text = "ВРЕМЕННЫЙ ЧЕРНОВИК"
        Check(editor.Text.StartsWith("● ") AndAlso FindControl(editor, "ApplyBankEditor").Enabled, "bank draft marks title and enables Apply")
        FindControl(editor, "BankAnswer0_0").Text = originalAnswer
        Check(Not editor.Dirty AndAlso Not FindControl(editor, "ApplyBankEditor").Enabled, "bank reverted values clear marker and disable Apply")
        DirectCast(FindControl(editor, "BankPeople1_0"), NumericUpDown).Value = 10
        FindControl(editor, "BankAnswer1_0").Text = "ОТВЕТ ДВОЙНОЙ ИГРЫ"
        Check(FindControl(editor, "BankAward1_0").Text = "20" AndAlso editor.Dirty, "editor uses raw people and shows multiplied award")
        Check(Not FindControl(editor, "LoadBankToConsole").Enabled, "unsaved bank cannot load into console")
        Check(Not FindControl(editor, "BankPeople3_0").Enabled, "reverse prices are read only")
        Dim path = IO.Path.Combine(directory, "edited.txt")
        editor.SaveDocument(path)
        Check(Not editor.Dirty AndAlso FindControl(editor, "LoadBankToConsole").Enabled, "successful save enables explicit load")
        DirectCast(FindControl(editor, "LoadBankToConsole"), Button).PerformClick()
        Check(FindControl(main, "LoadedBank").Text = "edited.txt", "loaded bank name visible")
        Check(GameRules.Clone(game.State).Main.Bank = before.Main.Bank AndAlso game.State.Main.Answers(0).Text = before.Main.Answers(0).Text, "saving and loading library do not alter match")
        editor.PerformLayout()
        Application.DoEvents()
        SaveBitmap(editor, IO.Path.Combine(directory, "BankEditor.png"))
        FindControl(editor, "BankAnswer0_0").Text = ""
        Reject(Sub() editor.SaveDocument(path), "UI validation preserves invalid draft")
        Check(editor.Dirty AndAlso FindControl(editor, "BankAnswer0_0").Text = "", "validation leaves editor input intact")
        PendingAnswer(editor, "ReviewChanges", Sub() editor.Close())
        Check(Not editor.IsDisposed AndAlso editor.Dirty, "closing editor can return to dirty draft")
        FindControl(editor, "BankAnswer0_0").Text = "СОХРАНИТЬ ПРИ ЗАКРЫТИИ"
        DirectCast(FindControl(editor, "OkBankEditor"), Button).PerformClick()
        Check(editor.IsDisposed AndAlso BankDocument.Open(path).Bank.Rounds(0)(0).Text = "СОХРАНИТЬ ПРИ ЗАКРЫТИИ", "closing editor can save draft")
        DirectCast(FindControl(main, "OpenBankEditor"), Button).PerformClick()
        editor = main.OwnedForms.OfType(Of BankEditor)().Single()
        editor.Opacity = 0
        editor.OpenDocument(path)
        FindControl(editor, "BankAnswer0_0").Text = "НЕ СОХРАНЯТЬ"
        PendingAnswer(editor, "DiscardChanges", Sub() editor.Close())
        Check(editor.IsDisposed AndAlso BankDocument.Open(path).Bank.Rounds(0)(0).Text = "СОХРАНИТЬ ПРИ ЗАКРЫТИИ", "closing editor can discard draft")
        DirectCast(FindControl(main, "OpenSettings"), Button).PerformClick()
        Dim settings = main.OwnedForms.OfType(Of AppearanceSettings)().Single()
        settings.Opacity = 0
        settings.ShowInTaskbar = False
        Dim secondaryScreen = Screen.AllScreens.FirstOrDefault(Function(screen) Not screen.Primary)
        If secondaryScreen IsNot Nothing Then settings.Location = secondaryScreen.WorkingArea.Location
        DirectCast(FindControl(main, "OpenSettings"), Button).PerformClick()
        Check(Screen.FromControl(settings).Primary, "reopening settings returns existing window to primary")
        Check(main.OwnedForms.OfType(Of AppearanceSettings)().Count() = 1, "settings button reuses window")
        Dim service = FieldValue(Of AppearanceService)(main, "_appearance")
        Dim rootTimer = FieldValue(Of Countdown)(main, "_countdown")
        Using board As New Tablo(), bigBoard As New BigGameTablo()
            board.Opacity = 0
            bigBoard.Opacity = 0
            board.Show()
            bigBoard.Show()
            GetType(Form1).GetField("_board", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).SetValue(main, board)
            GetType(Form1).GetField("_bigBoard", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).SetValue(main, bigBoard)
            board.Hide()
            bigBoard.Hide()
            FindControl(settings, "ProfileName").Text = "ПРОФИЛЬ МЕРОПРИЯТИЯ"
            DirectCast(FindControl(settings, "SettingsSound"), CheckBox).Checked = False
            Dim image = IO.Path.Combine(shipped, "Resources", "2026", "26_main.jpg")
            settings.SetAsset(0, image)
            settings.SetTextColor(Color.Navy)
            For i = 1 To 4
                settings.SetAsset(i, IO.Path.Combine(shipped, "Resources", "2026", New String() {"", "26_chance_l.png", "26_wrong_l.png", "26_chance_r.png", "26_wrong_r.png"}(i)))
            Next
            Check(service.Current.Profile.Main.Background = "", "draft image does not change active profile")
            Dim scope = DirectCast(FindControl(settings, "AppearanceScope"), ComboBox)
            scope.SelectedIndex = 2
            Check(Not FindControl(settings, "ChooseAsset0").Enabled, "inherited round locks override fields")
            DirectCast(FindControl(settings, "InheritAppearance"), CheckBox).Checked = False
            Check(FindControl(settings, "ChooseAsset0").Enabled, "explicit override unlocks round")
            settings.SetTextColor(Color.DarkRed)
            scope.SelectedIndex = 5
            settings.SetAsset(0, IO.Path.Combine(shipped, "Resources", "2026", "26_big.jpg"))
            settings.SetTextColor(Color.DarkGreen)
            Check(Not FindControl(settings, "ChooseAsset1").Enabled, "big settings cannot configure strikes")
            Dim lampDelay = DirectCast(FindControl(settings, "SettingsBuzzLampDelay"), NumericUpDown)
            lampDelay.Value = 750
            Check(FieldValue(Of BuzzManager)(main, "_buzz").LampDelayMilliseconds = 250, "lamp delay draft does not affect live receiver")
            scope.SelectedIndex = 0
            Dim undo = game.UndoDescription
            rootTimer.Start(25)
            settings.ApplyDraft()
            Check(service.Current.Profile.BuzzLampDelayMilliseconds = 750 AndAlso FieldValue(Of BuzzManager)(main, "_buzz").LampDelayMilliseconds = 750, "settings apply updates receiver lamp delay")
            Dim sample = game.State.Main
            sample.LeftScore = 999999
            sample.RightScore = 999999
            sample.LeftStrikes(0) = True
            sample.RightStrikes(0) = True
            board.Render(sample)
            Dim activeStyle = service.Current.Profile.MainStyle(sample.Round)
            Check(Object.ReferenceEquals(DirectCast(FindControl(board, "LeftStrike1Lbl"), PictureBox).Image, service.Current.Picture(activeStyle.LeftStrike, Nothing)), "left strike uses configured image")
            Check(Object.ReferenceEquals(DirectCast(FindControl(board, "LeftStrike2Lbl"), PictureBox).Image, service.Current.Picture(activeStyle.LeftClear, Nothing)), "left clear uses configured image")
            Check(Object.ReferenceEquals(DirectCast(FindControl(board, "RightStrike1Lbl"), PictureBox).Image, service.Current.Picture(activeStyle.RightStrike, Nothing)), "right strike uses configured image")
            Check(Object.ReferenceEquals(DirectCast(FindControl(board, "RightStrike2Lbl"), PictureBox).Image, service.Current.Picture(activeStyle.RightClear, Nothing)), "right clear uses configured image")
            Using graphics = board.CreateGraphics()
                For Each scoreName In New String() {"ScoreLeftLbl", "ScoreRightLbl"}
                    Dim label = FindControl(board, scoreName)
                    Check(graphics.MeasureString(label.Text, label.Font).Width <= label.Width, "six digit score fits spectator label " & scoreName)
                Next
            End Using
            Check(rootTimer.Running AndAlso game.UndoDescription = undo AndAlso StateXml(game.State) = StateXml(before), "apply preserves entire match history mask and running timer")
            Check(Not settings.Dirty AndAlso service.Current.Profile.Name = "ПРОФИЛЬ МЕРОПРИЯТИЯ", "apply resets settings dirty flag")
            Check(FindControl(board, "Answer1Lbl").ForeColor.ToArgb() = Color.Navy.ToArgb(), "main profile text color applied")
            Check(FindControl(bigBoard, "TimerLbl").ForeColor.ToArgb() = Color.DarkGreen.ToArgb(), "big timer receives independent profile color")
            game.Execute("double round appearance", Sub(state) state.Main.Round = 2)
            Check(FindControl(board, "Answer1Lbl").ForeColor.ToArgb() = Color.DarkRed.ToArgb(), "round switch applies override")
            game.Undo()
            Check(FindControl(board, "Answer1Lbl").ForeColor.ToArgb() = Color.Navy.ToArgb() AndAlso game.UndoDescription = undo, "round undo uses selected profile and inherited color")
            FindControl(settings, "ProfileName").Text = "ЧЕРНОВИК"
            PendingAnswer(settings, "ReviewChanges", Sub() DirectCast(FindControl(settings, "CancelSettings"), Button).PerformClick())
            Check(Not settings.IsDisposed AndAlso settings.Dirty AndAlso FindControl(settings, "ProfileName").Text = "ЧЕРНОВИК", "cancel can return to unchanged draft")
            FindControl(settings, "ProfileName").Text = "ПРОФИЛЬ МЕРОПРИЯТИЯ"
            Check(Not settings.Dirty AndAlso Not FindControl(settings, "ApplySettings").Enabled, "reverting values clears dirty marker")
            lampDelay.Value = 250
            settings.ApplyDraft()
            settings.PerformLayout()
            Application.DoEvents()
            SaveBitmap(settings, IO.Path.Combine(directory, "AppearanceSettings.png"))
            DirectCast(FindControl(settings, "SettingsTabs"), TabControl).SelectedIndex = 1
            Application.DoEvents()
            For Each commandName As String In New String() {"ApplySettings", "CancelSettings", "OkSettings"}
                Dim button = FindControl(settings, commandName)
                Dim bottom = settings.PointToClient(button.PointToScreen(New Point(0, button.Height))).Y
                Check(button.Visible AndAlso bottom <= settings.ClientSize.Height, "settings action stays visible on screen tab " & commandName)
            Next
            SaveBitmap(settings, IO.Path.Combine(directory, "DisplaySettings.png"))
            DirectCast(FindControl(settings, "SettingsTabs"), TabControl).SelectedIndex = 0
            For Each name In New String() {"PreviewAppearanceMain", "PreviewAppearanceSplash", "PreviewAppearanceBig"}
                Dim captured As Boolean
                main.BeginInvoke(New Action(Sub()
                                                Dim preview = Application.OpenForms.Cast(Of Form)().Single(Function(f) f.Name.StartsWith("AppearancePreview"))
                                                preview.Opacity = 0
                                                SaveBitmap(preview, IO.Path.Combine(directory, name & ".png"))
                                                preview.Close()
                                                captured = True
                                            End Sub))
                DirectCast(FindControl(settings, name), Button).PerformClick()
                Check(captured AndAlso rootTimer.Running AndAlso game.UndoDescription = undo, "preview isolated " & name)
            Next
            FindControl(settings, "ProfileName").Text = ""
            TestRootPendingCancel(main)
            Check(Not main.IsDisposed AndAlso main.Enabled AndAlso rootTimer.Running AndAlso settings.Dirty, "cancel pending settings retains whole application and timer")
            DirectCast(FindControl(settings, "ApplySettings"), Button).PerformClick()
            Check(Not settings.IsDisposed AndAlso settings.Dirty AndAlso service.Current.Profile.Name = "ПРОФИЛЬ МЕРОПРИЯТИЯ", "failed apply preserves dirty settings and current profile")
            DirectCast(FindControl(settings, "OkSettings"), Button).PerformClick()
            Check(Not settings.IsDisposed AndAlso settings.Dirty, "failed OK does not close settings")
            PendingAnswer(settings, "ReviewChanges", Sub() settings.Close())
            Check(Not settings.IsDisposed AndAlso rootTimer.Running, "cancel settings close leaves countdown running")
            PendingAnswer(settings, "DiscardChanges", Sub() settings.Close())
            Check(settings.IsDisposed AndAlso rootTimer.Running, "discard settings close preserves game timer")
            rootTimer.Stop()
            ' Ссылки на тестовые окна очищаются до последующей проверки graceful exit.
            GetType(Form1).GetField("_board", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).SetValue(main, Nothing)
            GetType(Form1).GetField("_bigBoard", Reflection.BindingFlags.Instance Or Reflection.BindingFlags.NonPublic).SetValue(main, Nothing)
        End Using
        TestCatalogAndAbout(main, directory)
        main.Hide()
    End Sub

    Private Sub CaptureAppearance(settings As AppearanceSettings, commandName As String, path As String)
        Dim captured As Boolean
        Dim started = Diagnostics.Stopwatch.StartNew()
        Using driver As New System.Windows.Forms.Timer With {.Interval = 20}
            AddHandler driver.Tick, Sub()
                                        If started.ElapsedMilliseconds > 5000 Then Throw New Exception("Appearance preview timeout")
                                        Dim preview = Application.OpenForms.Cast(Of Form)().SingleOrDefault(Function(f) f.Name.StartsWith("AppearancePreview") AndAlso f.Visible)
                                        If preview Is Nothing Then Return
                                        driver.Stop()
                                        preview.Opacity = 0
                                        Check(Screen.FromControl(preview).Primary, "appearance preview opens on primary screen")
                                        SaveBitmap(preview, path)
                                        preview.Close()
                                        captured = True
                                    End Sub
            driver.Start()
            DirectCast(FindControl(settings, commandName), Button).PerformClick()
        End Using
        Check(captured, "profile preview captured " & commandName)
    End Sub

    <StructLayout(LayoutKind.Sequential)>
    Private Structure DialogRect
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer
    End Structure
    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Function FindWindow(className As String, caption As String) As IntPtr
    End Function
    <DllImport("user32.dll", EntryPoint:="GetWindowRect")>
    Private Function NativeBounds(window As IntPtr, ByRef rect As DialogRect) As Boolean
    End Function

    <DllImport("kernel32.dll")>
    Private Function GetCurrentThreadId() As UInteger
    End Function
    <DllImport("user32.dll")>
    Private Function PostThreadMessage(thread As UInteger, message As UInteger, wParam As IntPtr, lParam As IntPtr) As Boolean
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Function SendMessage(window As IntPtr, message As UInteger, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    Private Delegate Function ChildWindowCallback(window As IntPtr, data As IntPtr) As Boolean
    <DllImport("user32.dll")>
    Private Function EnumChildWindows(window As IntPtr, callback As ChildWindowCallback, data As IntPtr) As Boolean
    End Function
    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Function GetWindowText(window As IntPtr, text As System.Text.StringBuilder, capacity As Integer) As Integer
    End Function
    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Function GetClassName(window As IntPtr, text As System.Text.StringBuilder, capacity As Integer) As Integer
    End Function
    <DllImport("user32.dll")>
    Private Function GetDlgCtrlID(window As IntPtr) As Integer
    End Function

    Private Sub TraceNativeDialog(window As IntPtr)
        Dim kind As New System.Text.StringBuilder(256)
        GetClassName(window, kind, kind.Capacity)
        Console.WriteLine("Dialog class: " & kind.ToString())
        EnumChildWindows(window, Function(child, data)
                                     Dim text As New System.Text.StringBuilder(512)
                                     Dim childKind As New System.Text.StringBuilder(256)
                                     GetWindowText(child, text, text.Capacity)
                                     GetClassName(child, childKind, childKind.Capacity)
                                     Console.WriteLine("Child: " & GetDlgCtrlID(child) & " | " & childKind.ToString() & " | " & text.ToString())
                                     Return True
                                 End Function, IntPtr.Zero)
    End Sub

    Private Sub TestNativePrimary(owner As Form)
        Dim started = Diagnostics.Stopwatch.StartNew()
        Dim timedOut As Boolean
        Dim title = "StoKOdnomu-primary-" & Guid.NewGuid().ToString("N")
        Dim checked As Boolean
        Dim onPrimary As Boolean
        Using driver As New System.Windows.Forms.Timer With {.Interval = 20}
            AddHandler driver.Tick, Sub()
                                        ' Уникальный заголовок адресует только диалог этого прогона.
                                        Dim window = FindWindow(Nothing, title)
                                        If started.ElapsedMilliseconds > 5000 Then
                                            Console.WriteLine("Native dialog timeout: title=" & title & "; handle=" & window.ToString())
                                            If window <> IntPtr.Zero Then TraceNativeDialog(window)
                                            timedOut = True
                                            driver.Stop()
                                            PostThreadMessage(GetCurrentThreadId(), &H12UI, IntPtr.Zero, IntPtr.Zero)
                                            Return
                                        End If
                                        If window = IntPtr.Zero Then Return
                                        Dim rect As DialogRect
                                        If Not NativeBounds(window, rect) Then Return
                                        ' Окно может существовать до завершения его инициализации.
                                        ' Ждём появления настоящей кнопки, затем вызываем её обработчик.
                                        onPrimary = Screen.PrimaryScreen.WorkingArea.Contains(Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom))
                                        Dim okButton As IntPtr
                                        ' У диалога одна кнопка. Внутренний control ID зависит
                                        ' от реализации Windows; выбираем реальный Button.
                                        EnumChildWindows(window, Function(child, data)
                                                                     Dim kind As New System.Text.StringBuilder(64)
                                                                     GetClassName(child, kind, kind.Capacity)
                                                                     If kind.ToString() = "Button" Then
                                                                         okButton = child
                                                                         Return False
                                                                     End If
                                                                     Return True
                                                                 End Function, IntPtr.Zero)
                                        If okButton = IntPtr.Zero Then Return
                                        checked = True
                                        SendMessage(okButton, &HF5UI, IntPtr.Zero, IntPtr.Zero)
                                    End Sub
            driver.Start()
            Console.WriteLine("UI: native primary-screen dialog")
            Dim result = OperatorWindows.Message(owner, "Проверка размещения системного диалога", title, MessageBoxButtons.OK, MessageBoxIcon.Information)
            Check(Not timedOut AndAlso checked AndAlso result = DialogResult.OK AndAlso onPrimary, "native dialog centers on primary even with secondary owner; found=" & checked & ", result=" & result.ToString() & ", primary=" & onPrimary & ", timeout=" & timedOut)
        End Using
    End Sub

    Private Sub TestCatalogAndAbout(main As Form1, directory As String)
        Dim errors As New List(Of String)()
        Dim choices = ProfileCatalog.Discover(ApplicationFiles.ProfilesDirectory, errors)
        Check(errors.Count = 0 AndAlso choices.Count = 2, "both complete bundled profiles discoverable next to binary")
        For Each choice In choices
            Dim profile = AppearanceStorage.Open(choice.ConfigFile)
            Check(profile.Main.Background <> "" AndAlso profile.Big.Background <> "" AndAlso profile.Main.Assets().All(Function(p) File.Exists(p)), "bundled profile owns six image files " & profile.Name)
            Check(profile.Main.TextArgb = If(profile.Name.EndsWith("2025"), Color.White.ToArgb(), Color.Black.ToArgb()), "bundled profile color " & profile.Name)
        Next
        Dim oldLocation = main.Location
        Dim secondary = Screen.AllScreens.FirstOrDefault(Function(screen) Not screen.Primary)
        If secondary IsNot Nothing Then main.Location = secondary.WorkingArea.Location
        Try
            Using service As New AppearanceService(Path.Combine(directory, "catalog-ui")), settings As New AppearanceSettings(service)
                Dim changes As Integer
                AddHandler service.Changed, Sub() changes += 1
                settings.Opacity = 0
                settings.ShowInTaskbar = False
                settings.Show(main)
                Application.DoEvents()
                Check(Screen.FromControl(settings).Primary, "settings opens on primary even with secondary owner")
                Check(Not settings.Dirty AndAlso Not FindControl(settings, "ApplySettings").Enabled, "clean Apply disabled")
                Dim cancel = FindControl(settings, "CancelSettings")
                Dim apply = FindControl(settings, "ApplySettings")
                Dim ok = FindControl(settings, "OkSettings")
                Check(cancel.Left < apply.Left AndAlso apply.Left < ok.Left, "footer has cancel left and apply OK right")
                SaveBitmap(settings, Path.Combine(directory, "SettingsClean.png"))
                Dim list = DirectCast(FindControl(settings, "ProfileList"), ComboBox)
                Dim index = Enumerable.Range(0, list.Items.Count).Single(Function(i) list.Items(i).ToString() = "Тупикон 2025")
                list.SelectedIndex = index
                Check(settings.Dirty AndAlso settings.Text.StartsWith("● ") AndAlso apply.Enabled, "selected profile marks draft with filled circle")
                Check(service.Current.Profile.Name = "Тупикон 2026", "selecting profile does not publish")
                SaveBitmap(settings, Path.Combine(directory, "SettingsDirty.png"))
                DirectCast(apply, Button).PerformClick()
                Check(changes = 1 AndAlso settings.Visible AndAlso Not settings.Dirty AndAlso Not apply.Enabled, "Apply publishes once without closing or confirmation")
                Check(service.Current.Profile.Name = "Тупикон 2025" AndAlso service.Current.Profile.Main.TextArgb = Color.White.ToArgb(), "2025 profile applied with white text")
                SaveBitmap(settings, Path.Combine(directory, "Settings2025.png"))
                CaptureAppearance(settings, "PreviewAppearanceMain", Path.Combine(directory, "Profile2025Main.png"))
                CaptureAppearance(settings, "PreviewAppearanceBig", Path.Combine(directory, "Profile2025Big.png"))
                list.SelectedIndex = Enumerable.Range(0, list.Items.Count).Single(Function(i) list.Items(i).ToString() = "Тупикон 2026")
                CaptureAppearance(settings, "PreviewAppearanceMain", Path.Combine(directory, "Profile2026Main.png"))
                DirectCast(ok, Button).PerformClick()
                Check(changes = 2 AndAlso settings.IsDisposed AndAlso service.Current.Profile.Name = "Тупикон 2026", "OK applies then closes")
                Using restarted As New AppearanceService(service.Folder)
                    Check(restarted.StartupError = "" AndAlso restarted.Current.Profile.Name = "Тупикон 2026", "catalog selection persists as self contained config")
                End Using
                Using clean As New AppearanceSettings(service)
                    clean.Opacity = 0
                    clean.Show(main)
                    DirectCast(FindControl(clean, "CancelSettings"), Button).PerformClick()
                    Check(clean.IsDisposed AndAlso changes = 2, "clean Cancel closes without dialog or applying")
                End Using
            End Using
            TestNativePrimary(main)
            Using about As New AboutBox1()
                about.Opacity = 0
                about.ShowInTaskbar = False
                about.Show(main)
                Application.DoEvents()
                Check(Screen.FromControl(about).Primary AndAlso about.Text = "О программе", "about opens on primary")
                Check(FindControl(about, "LabelProductName").Text = "Сто к одному" AndAlso FindControl(about, "LabelVersion").Text.Contains(GetType(AboutBox1).Assembly.GetName().Version.ToString(3)), "about uses actual product and assembly version")
                Check(FindControl(about, "LabelCompanyName").Text.Contains("MIT") AndAlso FindControl(about, "LabelCompanyName").Text.Contains("Vasily Makrov"), "about displays MIT and copyright holder")
                Check(Not FindControl(about, "TextBoxDescription").Text.Contains("git clone"), "about description matches product")
                Dim run = FindControl(about, "RunLbl")
                Check(run.Text = "Run..." AndAlso run.Width >= 100 AndAlso run.Height >= 40, "Run label and larger hit area preserved")
                SaveBitmap(about, Path.Combine(directory, "About.png"))
            End Using
        Finally
            main.Location = oldLocation
        End Try
    End Sub

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
        For Each file In StabilityChecks.BankNames.Select(Function(name) IO.Path.Combine(source, name))
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
        Check(Not BuzzManager.LampIsLit(0, 250) AndAlso Not BuzzManager.LampIsLit(249, 250), "lamp remains off before delay")
        Check(BuzzManager.LampIsLit(250, 250) AndAlso BuzzManager.LampIsLit(374, 250), "first flash starts after full delay")
        Check(Not BuzzManager.LampIsLit(375, 250) AndAlso BuzzManager.LampIsLit(500, 250), "flash phase is relative to delayed start")
        Check(Not BuzzManager.LampIsLit(15249, 250) AndAlso Not BuzzManager.LampIsLit(15250, 250), "blink ends 15 seconds after delayed start")
        Check(BuzzManager.LampIsLit(0, 0) AndAlso Not BuzzManager.LampIsLit(15000, 0), "zero delay starts immediately and still expires")
        Check(Not BuzzManager.LampIsLit(4999, 5000) AndAlso BuzzManager.LampIsLit(5000, 5000), "maximum configured delay")
    End Sub

    Private NotInheritable Class FakeReceiver
        Implements IBuzzReceiver
        Public ReadOnly Inputs As New BlockingCollection(Of Integer)()
        Public Failure As Exception
        Public LampWorks As Boolean = True
        Public DisposedCount As Integer
        Public ReadOnly LitPads As New ConcurrentQueue(Of Integer)()
        Public BeforeRead As Action

        Public Function ReadButtons() As Integer? Implements IBuzzReceiver.ReadButtons
            If BeforeRead IsNot Nothing Then BeforeRead()
            If Failure IsNot Nothing Then Throw Failure
            Dim mask As Integer
            If Inputs.TryTake(mask, 20) Then Return mask
            Return Nothing
        End Function
        Public Function SetLamp(pad As Integer, lit As Boolean) As Boolean Implements IBuzzReceiver.SetLamp
            If lit Then LitPads.Enqueue(pad)
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
        Dim delayedReceiver As New FakeReceiver()
        Using manager As New BuzzManager(Function() delayedReceiver)
            manager.LampDelayMilliseconds = 5000
            manager.ConnectAsync().GetAwaiter().GetResult()
            manager.Arm()
            delayedReceiver.Inputs.Add(1)
            WaitFor(Function() manager.Status.State = BuzzState.Winner, "delay does not postpone winner")
            manager.LampDelayMilliseconds = 0
            Thread.Sleep(40)
            Check(delayedReceiver.LitPads.IsEmpty, "changing setting does not move pending winner lamp")
            manager.Disarm()
            delayedReceiver.Inputs.Add(0)
            Thread.Sleep(40)
            Check(delayedReceiver.LitPads.IsEmpty, "disarm cancels pending lamp")
            manager.Arm()
            delayedReceiver.Inputs.Add(2)
            WaitFor(Function() delayedReceiver.LitPads.Contains(1), "next winner uses zero delay")
            manager.ShutdownAsync().GetAwaiter().GetResult()
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
                    If form Is main OrElse form Is big Then Check(Screen.FromControl(form).Primary, "operator opens on primary screen " & form.Name)
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
                TestBigTotalPosition(bigBoard, directory)
            End Using
            TestPreparationUi(main, directory)
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
        Environment.SetEnvironmentVariable("STOKODNOMU_DATA_DIR", Path.Combine(directory, "ui-fast"))
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
                                        Check(Screen.FromControl(dialog).Primary, "exit dialog opens on primary screen")
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

    Private Sub TestBigTotalPosition(board As BigGameTablo, directory As String)
        Dim game = Session(directory, "moving-big-total")
        Dim render As EventHandler = Sub() board.Render(game.State.Big)
        AddHandler game.Changed, render
        Try
            board.ClientSize = New Size(1264, 681)
            board.Show()
            board.RenderTimer(17, True)
            game.Execute("first participant", Sub(state)
                                                  For i = 0 To 4
                                                      GameRules.ShowBigText(state.Big, i, New String() {"СЕМЬЯ", "ДРУЗЬЯ", "ДОМ", "РАБОТА", "ОТДЫХ"}(i))
                                                      GameRules.ShowBigPoints(state.Big, i, (i + 1) * 10)
                                                  Next
                                              End Sub)
            Dim caption = FindControl(board, "TotalTxt")
            Dim total = FindControl(board, "TotalLbl")
            Dim leftCaption = caption.Bounds
            Dim leftTotal = total.Bounds
            Dim header = FindControl(board, "RoundScoreLbl").Bounds
            Check(total.Text = "150" AndAlso total.Right = FindControl(board, "Answer1ScoreLbl").Right, "first participant total is beneath left list")
            SaveBitmap(board, Path.Combine(directory, "BigTotalLeft.png"))
            game.Execute("second draft and first mask", Sub(state)
                                                           state.Big.Answers(5).Text = "ЧЕРНОВИК"
                                                           state.Big.HideFirstPlayer = True
                                                       End Sub)
            Check(caption.Bounds = leftCaption AndAlso total.Bounds = leftTotal, "draft and mask do not move total")
            game.Execute("first text of second participant", Sub(state) GameRules.ShowBigText(state.Big, 5, "ПРАЗДНИК"))
            Dim rightCaption = caption.Bounds
            Dim rightTotal = total.Bounds
            Check(total.Left > leftTotal.Left AndAlso total.Right = FindControl(board, "Answer6ScoreLbl").Right, "first published second-player text moves total to right list")
            Check(rightCaption.X - leftCaption.X = rightTotal.X - leftTotal.X AndAlso rightTotal.Y = leftTotal.Y, "caption and value move together horizontally")
            Check(total.Text = "150" AndAlso FindControl(board, "RoundScoreLbl").Bounds = header, "moving total does not award points or move central score")
            Check(FindControl(board, "Answer6Lbl").Visible AndAlso Not FindControl(board, "Answer6ScoreLbl").Visible, "move happens on text before score reveal")
            SaveBitmap(board, Path.Combine(directory, "BigTotalRight.png"))
            Dim restored = Session(directory, "moving-big-total")
            restored.Restore()
            board.Render(restored.State.Big)
            Check(caption.Bounds = rightCaption AndAlso total.Bounds = rightTotal, "restored match derives right total position from answers")
            game.Execute("unmask first participant", Sub(state) state.Big.HideFirstPlayer = False)
            Check(caption.Bounds = rightCaption AndAlso total.Bounds = rightTotal, "unmasking first participant does not return total")
            game.Undo()
            game.Execute("second score", Sub(state) GameRules.ShowBigPoints(state.Big, 5, 40))
            Check(total.Text = "190" AndAlso total.Bounds = rightTotal, "second-player score updates existing right total")
            game.Undo()
            Check(total.Text = "150" AndAlso total.Bounds = rightTotal, "undo points keeps total right while text remains")
            game.Undo()
            Check(caption.Bounds = leftCaption AndAlso total.Bounds = leftTotal, "undo first second-player text returns total left")
            game.Execute("out of order second text", Sub(state) GameRules.ShowBigText(state.Big, 8, "МУЗЫКА"))
            Check(total.Bounds = rightTotal, "direct reveal of another second-player row also moves total")
            Dim rightState = game.State.Big
            Dim leftState As New BigGameState()
            For Each size In New Size() {New Size(640, 380), New Size(1264, 681), New Size(1920, 1080), New Size(800, 1000)}
                board.ClientSize = size
                For Each state In New BigGameState() {leftState, rightState}
                    Dim snapshot As New MatchState With {.Big = state}
                    Dim before = StateXml(snapshot)
                    board.Render(state)
                    board.RenderTimer(16, True)
                    Dim price = FindControl(board, If(state Is rightState, "Answer6ScoreLbl", "Answer1ScoreLbl"))
                    Check(Math.Abs(total.Right - price.Right) <= 1 AndAlso caption.Top = total.Top, "moving total scales and stays aligned at " & size.ToString())
                    Check(caption.Top >= FindControl(board, "Answer10Lbl").Bottom, "total stays below answers at " & size.ToString())
                    Check(StateXml(snapshot) = before, "position render has no model side effects at " & size.ToString())
                Next
            Next
            board.ClientSize = New Size(1264, 681)
            board.Render(rightState)
            Using appearance As New LoadedAppearance(New AppearanceProfile())
                board.ApplyAppearance(appearance)
                board.RenderTimer(15, True)
                Check(total.Bounds = rightTotal, "appearance and timer refresh retain moving total position")
            End Using
            game.Execute("new big game", Sub(state) state.Big = New BigGameState())
            Check(total.Bounds = leftTotal AndAlso total.Text = "0", "new big game resets total left")
            game.Undo()
            Check(total.Bounds = rightTotal, "undo new big game restores total right")
        Finally
            RemoveHandler game.Changed, render
            board.Hide()
        End Try
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

    ' WM_CHAR направлен конкретному редактору NumericUpDown, а не активному
    ' окну рабочего стола: проверяем настоящий ввод, не перехватывая клавиатуру.
    <DllImport("user32.dll", EntryPoint:="SendMessageW", CharSet:=CharSet.Unicode, ExactSpelling:=True)>
    Private Function SendWindowMessage(window As IntPtr, message As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function

    Private Sub TestBigTabOrder(big As BigGameControl)
        big.Activate()
        Dim processKey = GetType(BigGameControl).GetMethod("ProcessDialogKey", Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Instance)
        For i = 0 To 9
            Dim answer = FindControl(big, "BigAnswer" & i)
            Dim points = DirectCast(FindControl(big, "BigPoints" & i), NumericUpDown)
            Dim original = points.Value
            points.Value = 0
            answer.Focus()
            processKey.Invoke(big, New Object() {Keys.Tab})
            Check(points.ContainsFocus, "Tab moves answer to its points: " & i)
            Dim edit = points.Controls.OfType(Of TextBox)().Single()
            Check(edit.SelectionStart = 0 AndAlso edit.SelectionLength = edit.Text.Length, "Tab selects entire zero: " & i)
            SendWindowMessage(edit.Handle, &H102, New IntPtr(AscW("7"c)), IntPtr.Zero)
            processKey.Invoke(big, New Object() {Keys.Tab})
            Check(points.Value = 7, "typing 7 replaces zero rather than producing 70: " & i)
            processKey.Invoke(big, New Object() {Keys.Tab Or Keys.Shift})
            Check(points.ContainsFocus AndAlso edit.SelectionLength = edit.Text.Length, "Shift+Tab selects entire score: " & i)
            points.Value = 125
            answer.Focus()
            processKey.Invoke(big, New Object() {Keys.Tab})
            SendWindowMessage(edit.Handle, &H102, New IntPtr(AscW("4"c)), IntPtr.Zero)
            SendWindowMessage(edit.Handle, &H102, New IntPtr(AscW("2"c)), IntPtr.Zero)
            processKey.Invoke(big, New Object() {Keys.Tab})
            Check(points.Value = 42, "typing 42 replaces an existing three-digit score: " & i)
            processKey.Invoke(big, New Object() {Keys.Tab Or Keys.Shift})
            points.Value = original
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
