Option Strict On
Imports System.Drawing
Imports System.IO
Imports System.Reflection
Imports System.Windows.Forms
Imports StoKOdnomuControl

' Снимки настоящих форм: отдельный процесс, временный матч, без USB и звука.
' Демонстрационные числа не являются результатами опросов.
Public Module ScreenshotCapture
    Public Sub Run(destination As String, dataDirectory As String)
        Application.EnableVisualStyles()
        Directory.CreateDirectory(destination)
        Environment.SetEnvironmentVariable("STOKODNOMU_DATA_DIR", dataDirectory)
        AudioService.Instance.Enabled = False
        Using buzz As New BuzzManager(Function() Nothing), control As New Form1(buzz)
            Prepare(control)
            control.Show()
            Application.DoEvents()
            Dim flags = BindingFlags.Instance Or BindingFlags.NonPublic
            Dim session = DirectCast(GetType(Form1).GetField("_session", flags).GetValue(control), GameSession)
            Dim countdown = DirectCast(GetType(Form1).GetField("_countdown", flags).GetValue(control), Countdown)
            Dim bankPath = Path.Combine(ApplicationFiles.BanksDirectory, "demo-basic.txt")
            Dim bank = BankDocument.Open(bankPath).Bank
            GetType(Form1).GetMethod("LoadBankFile", flags).Invoke(control, New Object() {bankPath})
            session.Execute("Демонстрационные ответы", Sub(state)
                                                           state.Main.Round = 1
                                                           state.Main.LeftScore = 240
                                                           state.Main.RightScore = 180
                                                           For i = 0 To 5
                                                               Dim answer = bank.Rounds(0)(i)
                                                               state.Main.Answers(i).Text = answer.Text
                                                               state.Main.Answers(i).Points = answer.Points
                                                               If i < 3 Then GameRules.ShowMainAnswer(state.Main, i, answer.Text, answer.Points, True)
                                                           Next
                                                           state.Main.LeftStrikes(0) = True
                                                           state.Main.RightStrikes(0) = True
                                                           state.Main.RightStrikes(1) = True
                                                           Dim texts = {"КНИГА", "ПРОГУЛКА", "МУЗЫКА", "ФИЛЬМ", "ЧАЙ", "КОФЕ", "ПОЕЗДКА", "ИГРА", "ВСТРЕЧА", "ОТДЫХ"}
                                                           Dim points = {30, 25, 20, 15, 10, 20, 15, 10, 0, 5}
                                                           For i = 0 To 9
                                                               GameRules.ShowBigText(state.Big, i, texts(i))
                                                               GameRules.ShowBigPoints(state.Big, i, points(i))
                                                           Next
                                                       End Sub)
            Save(control, Path.Combine(destination, "operator-main.png"))
            Using bigControl As New BigGameControl(session, countdown, Sub() Return, Sub() session.Undo()),
                  appearance As New LoadedAppearance(New AppearanceProfile()),
                  main As New Tablo(True), big As New BigGameTablo(True)
                Prepare(bigControl)
                bigControl.Show()
                Save(bigControl, Path.Combine(destination, "operator-big.png"))
                For Each board As Form In New Form() {main, big}
                    Prepare(board)
                    board.FormBorderStyle = FormBorderStyle.None
                    board.Show()
                    board.ClientSize = New Size(1920, 1080)
                Next
                main.ApplyAppearance(appearance, session.State.Main.Round)
                main.Render(session.State.Main)
                big.ApplyAppearance(appearance)
                big.Render(session.State.Big)
                big.RenderTimer(17, True)
                Save(main, Path.Combine(destination, "board-main.png"))
                Save(big, Path.Combine(destination, "board-big.png"))
            End Using
        End Using
        Console.WriteLine("DrawToBitmap screenshots: " & Path.GetFullPath(destination))
    End Sub

    Private Sub Prepare(form As Form)
        form.Opacity = 0
        form.ShowInTaskbar = False
    End Sub

    Private Sub Save(form As Form, file As String)
        form.PerformLayout()
        Application.DoEvents()
        Using bitmap As New Bitmap(form.Width, form.Height)
            form.DrawToBitmap(bitmap, New Rectangle(Point.Empty, bitmap.Size))
            bitmap.Save(file)
        End Using
    End Sub
End Module
