Option Strict On
' Зрительское окно только рисует снимок. Здесь нет начислений, Undo и звуков,
' поэтому повторный показ окна не меняет игру и не повторяет эффекты.
Public Class Tablo
    Private ReadOnly _splash As SplashOverlay
    Private ReadOnly _layout As BoardLayout
    Private ReadOnly _texts As Label()
    Private ReadOnly _scores As Label()
    Private ReadOnly _left As PictureBox()
    Private ReadOnly _right As PictureBox()

    Public Sub New()
        Me.New(False)
    End Sub

    Public Sub New(preview As Boolean)
        InitializeComponent()
        ' Designer сохраняет геометрию; массивы заменяют одинаковые ветви рендера.
        ' Картинки берутся из общих ресурсов, без шести копий в Tablo.resx.
        _texts = {Answer1Lbl, Answer2Lbl, Answer3Lbl, Answer4Lbl, Answer5Lbl, Answer6Lbl}
        _scores = {Answer1ScoreLbl, Answer2ScoreLbl, Answer3ScoreLbl, Answer4ScoreLbl, Answer5ScoreLbl, Answer6ScoreLbl}
        _left = {LeftStrike1Lbl, LeftStrike2Lbl, LeftStrike3Lbl}
        _right = {RightStrike1Lbl, RightStrike2Lbl, RightStrike3Lbl}
        _splash = SplashOverlay.AddTo(Me, _texts.Cast(Of Control)().Concat(_scores))
        _layout = New BoardLayout(Me, Not preview)
        AddHandler Disposed, Sub() _layout.Dispose()
        Render(New MainGameState())
    End Sub

    Public Sub ShowOn(screen As Screen, fullscreen As Boolean)
        _layout.ShowOn(screen, fullscreen)
    End Sub

    Public Sub Render(state As MainGameState)
        ' Полностью восстанавливаем картину из модели, затем накладываем заставку.
        ' Возврат к игре не требует считывания значений с пульта.
        ScoreLeftLbl.Text = state.LeftScore.ToString()
        ScoreRightLbl.Text = state.RightScore.ToString()
        RoundScoreLbl.Text = state.Bank.ToString()
        Dim roundNames = {"", "1", "x2", "x3", "?"}
        LeftRoundLbl.Text = roundNames(state.Round)
        RightRoundLbl.Text = roundNames(state.Round)
        For i = 0 To 5
            Dim answer = state.Answers(i)
            _texts(i).Visible = Not state.SplashScreen.Visible
            _scores(i).Visible = Not state.SplashScreen.Visible
            _texts(i).TextAlign = ContentAlignment.MiddleLeft
            _texts(i).Text = (i + 1).ToString() & "." & If(answer.TextVisible, " " & answer.Text, "")
            _scores(i).Text = If(answer.TextVisible, answer.Points.ToString(), "")
        Next
        For i = 0 To 2
            _left(i).Image = If(state.LeftStrikes(i), My.Resources._26_wrong_l, My.Resources._26_chance_l)
            _right(i).Image = If(state.RightStrikes(i), My.Resources._26_wrong_r, My.Resources._26_chance_r)
        Next
        _splash.Render(state.SplashScreen)
        _layout.RefreshText()
    End Sub
End Class
