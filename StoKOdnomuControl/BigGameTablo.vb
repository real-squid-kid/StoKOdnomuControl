Option Strict On
Public Class BigGameTablo
    Private ReadOnly _layout As BoardLayout
    Private ReadOnly _texts As Label()
    Private ReadOnly _scores As Label()

    Public Sub New()
        InitializeComponent()
        _texts = {Answer1Lbl, Answer2Lbl, Answer3Lbl, Answer4Lbl, Answer5Lbl, Answer6Lbl, Answer7Lbl, Answer8Lbl, Answer9Lbl, Answer10Lbl}
        _scores = {Answer1ScoreLbl, Answer2ScoreLbl, Answer3ScoreLbl, Answer4ScoreLbl, Answer5ScoreLbl, Answer6ScoreLbl, Answer7ScoreLbl, Answer8ScoreLbl, Answer9ScoreLbl, Answer10ScoreLbl}
        _layout = New BoardLayout(Me)
        AddHandler Disposed, Sub() _layout.Dispose()
        Render(New BigGameState())
    End Sub

    Public Sub ShowOn(screen As Screen, fullscreen As Boolean)
        _layout.ShowOn(screen, fullscreen)
    End Sub

    Public Sub Render(state As BigGameState)
        TotalTxt.Visible = True
        TotalLbl.Visible = True
        RoundScoreLbl.Visible = True
        TotalLbl.Text = state.Total.ToString()
        RoundScoreLbl.Text = state.Total.ToString()
        For i = 0 To 9
            Dim answer = state.Answers(i)
            Dim masked = i < 5 AndAlso state.HideFirstPlayer
            _texts(i).Visible = answer.TextVisible
            _scores(i).Visible = answer.ScoreVisible
            _texts(i).Text = If(masked, "••••••••", answer.Text)
            _scores(i).Text = If(masked, "••", answer.Points.ToString())
        Next
        _layout.RefreshText()
    End Sub

    Public Sub RenderTimer(seconds As Integer, visible As Boolean)
        TimerLbl.Text = seconds.ToString()
        TimerLbl.Visible = visible
        _layout.RefreshText()
    End Sub
End Class
