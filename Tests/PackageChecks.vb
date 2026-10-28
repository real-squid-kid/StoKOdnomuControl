Option Strict On
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports StoKOdnomuControl

Public Module PackageChecks
    <DllImport("user32.dll")>
    Private Function GetGuiResources(process As IntPtr, flags As UInteger) As UInteger
    End Function

    Private Sub Collect()
        GC.Collect()
        GC.WaitForPendingFinalizers()
        GC.Collect()
    End Sub

    Public Sub SoakUi(directory As String, check As Action(Of Boolean, String))
        Dim profile As New AppearanceProfile()
        Using loaded As New LoadedAppearance(profile), main As New Tablo(True), big As New BigGameTablo(True)
            main.Opacity = 0
            big.Opacity = 0
            main.ShowInTaskbar = False
            big.ShowInTaskbar = False
            main.Show()
            big.Show()
            main.ApplyAppearance(loaded, 1)
            big.ApplyAppearance(loaded)
            Dim state As New MatchState()
            GameRules.ShowMainAnswer(state.Main, 0, New String("Ш"c, 110), 60, True)
            GameRules.ShowBigText(state.Big, 5, "ДЛИННЫЙ ОТВЕТ ВТОРОГО УЧАСТНИКА")
            GameRules.ShowBigPoints(state.Big, 5, 50)
            For i = 0 To 19
                main.Render(state.Main)
                big.Render(state.Big)
                big.RenderTimer(i, True)
                Application.DoEvents()
            Next
            Collect()
            Dim currentProcess = Diagnostics.Process.GetCurrentProcess()
            Dim gdiBefore = GetGuiResources(currentProcess.Handle, 0)
            Dim userBefore = GetGuiResources(currentProcess.Handle, 1)
            Dim memoryBefore = GC.GetTotalMemory(False)
            currentProcess.Refresh()
            Dim privateBefore = currentProcess.PrivateMemorySize64
            Dim watch = Stopwatch.StartNew()
            For i = 0 To 299
                Dim size = If(i Mod 2 = 0, New Size(800, 450), New Size(1264, 681))
                main.ClientSize = size
                big.ClientSize = size
                main.Render(state.Main)
                big.Render(state.Big)
                big.RenderTimer(i Mod 26, True)
                If i Mod 20 = 0 Then
                    Using service As New AppearanceService(IO.Path.Combine(directory, "window-soak")), settings As New AppearanceSettings(service)
                        settings.Opacity = 0
                        settings.ShowInTaskbar = False
                        settings.Show()
                        Application.DoEvents()
                        settings.Close()
                    End Using
                End If
                Application.DoEvents()
                If i Mod 50 = 49 Then Console.WriteLine("UI soak progress: " & (i + 1) & "/300")
            Next
            Collect()
            Dim gdiAfter = GetGuiResources(currentProcess.Handle, 0)
            Dim userAfter = GetGuiResources(currentProcess.Handle, 1)
            Dim memoryAfter = GC.GetTotalMemory(False)
            currentProcess.Refresh()
            Dim privateAfter = currentProcess.PrivateMemorySize64
            check(privateAfter - privateBefore < 128L * 1024 * 1024, "window cycles do not accumulate native image memory")
            check(CLng(gdiAfter) - gdiBefore < 30 AndAlso CLng(userAfter) - userBefore < 30, "300 redraw and resize cycles do not accumulate GDI or USER handles")
            check(memoryAfter - memoryBefore < 16L * 1024 * 1024, "window cycles do not retain growing managed object graph")
            Console.WriteLine("UI soak: 300 cycles, " & watch.Elapsed.TotalSeconds.ToString("F1", Globalization.CultureInfo.InvariantCulture) & " s; GDI " & gdiBefore & " -> " & gdiAfter & "; USER " & userBefore & " -> " & userAfter & "; managed bytes " & memoryBefore & " -> " & memoryAfter & "; private bytes " & privateBefore & " -> " & privateAfter)
        End Using
    End Sub

    ' Этот режим запускается копией проверочного EXE рядом с ПЕРЕНЕСЁННЫМ комплектом.
    ' Поэтому CLR не может подставить DLL, шрифты или профили из рабочего bin проекта.
    Public Sub Run(reportPath As String)
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException)
        Application.EnableVisualStyles()
        Dim checks As Integer
        Dim check As Action(Of Boolean, String) = Sub(condition, description)
                                                     If Not condition Then Throw New Exception("Package FAIL: " & description)
                                                     checks += 1
                                                 End Sub
        Dim root = AppDomain.CurrentDomain.BaseDirectory
        Dim reports = IO.Path.GetDirectoryName(IO.Path.GetFullPath(reportPath))
        IO.Directory.CreateDirectory(reports)
        Environment.SetEnvironmentVariable("STOKODNOMU_DATA_DIR", IO.Path.Combine(reports, "data"))
        AudioService.Instance.Enabled = False
        check(String.Equals(GetType(Form1).Assembly.Location, IO.Path.Combine(root, "StoKOdnomuControl.exe"), StringComparison.OrdinalIgnoreCase), "application assembly loaded from relocated package")
        check(Not String.Equals(IO.Path.GetFullPath(Environment.CurrentDirectory).TrimEnd(IO.Path.DirectorySeparatorChar), root.TrimEnd(IO.Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase), "working directory differs from executable directory")
        For Each file In IO.Directory.GetFiles(ApplicationFiles.LibraryDirectory, "*.dll")
            ' Загрузка по имени проверяет CLR probing из .config; LoadFrom здесь
            ' мог бы скрыть сломанную настройку поиска зависимостей.
            Dim dependency = Assembly.Load(AssemblyName.GetAssemblyName(file))
            check(String.Equals(dependency.Location, file, StringComparison.OrdinalIgnoreCase), "load from lib " & IO.Path.GetFileName(file))
        Next
        For Each name In StabilityChecks.BankNames
            Dim bank = BankDocument.Open(IO.Path.Combine(ApplicationFiles.BanksDirectory, name))
            check(QuestionBankReader.Parse(BankDocument.Serialize(bank.Bank)).Rounds.Length = 4, "packaged bank " & name)
        Next
        check(File.Exists(IO.Path.Combine(ApplicationFiles.FontsDirectory, "JetBrainsMono-Regular.ttf")) AndAlso File.Exists(IO.Path.Combine(ApplicationFiles.FontsDirectory, "JetBrainsMono-Bold.ttf")) AndAlso File.Exists(IO.Path.Combine(ApplicationFiles.FontsDirectory, "OFL.txt")), "both fonts and OFL supplied")
        check(File.Exists(IO.Path.Combine(ApplicationFiles.LibraryDirectory, "THIRD_PARTY_NOTICES.txt")) AndAlso File.Exists(IO.Path.Combine(ApplicationFiles.LibraryDirectory, "licenses", "NAudio.txt")), "third-party notices supplied")
        check(IO.Directory.GetFiles(root, "*.dll").Length = 0, "no runtime DLLs beside executable")
        For Each name In {"README.MD", "OPERATOR_GUIDE.MD", "ARCHITECTURE.MD", "VERIFY.md", "LICENSE"}
            check(File.Exists(IO.Path.Combine(ApplicationFiles.LibraryDirectory, "docs", name)), "packaged documentation " & name)
        Next
        check(File.ReadAllText(IO.Path.Combine(ApplicationFiles.LibraryDirectory, "docs", "LICENSE")).Contains("Vasily Makrov"), "MIT copyright included")
        Dim errors As New List(Of String)()
        Dim themes = ProfileCatalog.Discover(ApplicationFiles.ProfilesDirectory, errors)
        check(errors.Count = 0 AndAlso themes.Count = 2, "both themes load after folder relocation")
        ' Декодирование проверяет все WAV и зависимости NAudio, не открывая аудиоустройство.
        Dim decode = GetType(AudioService).GetMethod("Decode", BindingFlags.Instance Or BindingFlags.NonPublic)
        For Each cue As SoundCue In [Enum].GetValues(GetType(SoundCue))
            Dim samples = DirectCast(decode.Invoke(AudioService.Instance, New Object() {cue}), Single())
            check(samples.Length > 0, "packaged audio decodes " & cue.ToString())
        Next
        Using buzz As New BuzzManager(Function() Nothing), control As New Form1(buzz), main As New Tablo(True), big As New BigGameTablo(True)
            For Each form As Form In New Form() {control, main, big}
                form.Opacity = 0
                form.ShowInTaskbar = False
                form.Show()
            Next
            Application.DoEvents()
            check(Screen.FromControl(control).Primary, "operator starts on primary monitor")
            check(Find(main, "Answer1Lbl").Font.FontFamily.Name.Contains("JetBrains"), "private packaged font used")
            Save(control, IO.Path.Combine(reports, "package-operator.png"))
            For Each choice In themes
                Using appearance As New LoadedAppearance(AppearanceStorage.Open(choice.ConfigFile))
                    Dim state As New MatchState()
                    GameRules.ShowMainAnswer(state.Main, 0, "ПРОВЕРКА ПЕРЕНОСА КОМПЛЕКТА", 42, True)
                    GameRules.ShowBigText(state.Big, 5, "ВТОРОЙ УЧАСТНИК")
                    GameRules.ShowBigPoints(state.Big, 5, 50)
                    main.ApplyAppearance(appearance, 1)
                    main.Render(state.Main)
                    big.ApplyAppearance(appearance)
                    big.Render(state.Big)
                    big.RenderTimer(17, True)
                    Application.DoEvents()
                    Dim suffix = If(choice.Name.EndsWith("2025"), "2025", "2026")
                    Save(main, IO.Path.Combine(reports, "package-main-" & suffix & ".png"))
                    Save(big, IO.Path.Combine(reports, "package-big-" & suffix & ".png"))
                    check(Find(big, "TotalLbl").Left > Find(big, "Answer6Lbl").Left, "moving total preserved in packaged theme " & suffix)
                    main.Hide()
                    big.Hide()
                End Using
            Next
            DirectCast(Find(control, "OpenSettings"), Button).PerformClick()
            Dim settings = control.OwnedForms.OfType(Of AppearanceSettings)().Single()
            settings.Opacity = 0
            check(DirectCast(Find(settings, "ProfileList"), ComboBox).Items.Count = 3, "settings discovers relocated themes")
            Save(settings, IO.Path.Combine(reports, "package-settings.png"))
            settings.Close()
        End Using
        File.WriteAllText(reportPath, "PASS: " & checks & " relocated-package checks" & Environment.NewLine & "CLR: " & Environment.Version.ToString() & Environment.NewLine & "Working directory: " & Environment.CurrentDirectory & Environment.NewLine & "Package directory: " & root & Environment.NewLine & "Hardware and clean-OS testing are not performed by this check.")
        Console.WriteLine("PASS: " & checks & " relocated-package checks")
    End Sub

    Private Function Find(parent As Control, name As String) As Control
        If parent.Name = name Then Return parent
        For Each child As Control In parent.Controls
            Dim found = Find(child, name)
            If found IsNot Nothing Then Return found
        Next
        Return Nothing
    End Function

    Private Sub Save(form As Form, path As String)
        form.PerformLayout()
        Using image As New Bitmap(form.Width, form.Height)
            form.DrawToBitmap(image, New Rectangle(Point.Empty, image.Size))
            image.Save(path)
        End Using
    End Sub
End Module
