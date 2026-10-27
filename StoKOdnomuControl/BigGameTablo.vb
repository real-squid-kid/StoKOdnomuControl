Option Strict On
' Рендер не читает поля BigGameControl и не считает итог самостоятельно.
Public Class BigGameTablo
    Private ReadOnly _layout As BoardLayout
    Private ReadOnly _texts As Label()
    Private ReadOnly _scores As Label()
    Private ReadOnly _totalCaptionLeft As Rectangle
    Private ReadOnly _totalValueLeft As Rectangle
    Private ReadOnly _totalRightOffset As Integer

    Public Sub New()
        Me.New(False)
    End Sub

    Public Sub New(preview As Boolean)
        InitializeComponent()
        _texts = {Answer1Lbl, Answer2Lbl, Answer3Lbl, Answer4Lbl, Answer5Lbl, Answer6Lbl, Answer7Lbl, Answer8Lbl, Answer9Lbl, Answer10Lbl}
        _scores = {Answer1ScoreLbl, Answer2ScoreLbl, Answer3ScoreLbl, Answer4ScoreLbl, Answer5ScoreLbl, Answer6ScoreLbl, Answer7ScoreLbl, Answer8ScoreLbl, Answer9ScoreLbl, Answer10ScoreLbl}
        ' Запоминаем геометрию до масштабирования. Справа итог выравнивается
        ' по правому краю цен второго участника; обе подписи перемещаются вместе.
        _totalCaptionLeft = TotalTxt.Bounds
        _totalValueLeft = TotalLbl.Bounds
        _totalRightOffset = Answer6ScoreLbl.Right - Answer1ScoreLbl.Right
        _layout = New BoardLayout(Me, Not preview)
        AddHandler Disposed, Sub() _layout.Dispose()
        Render(New BigGameState())
    End Sub

    Public Sub ShowOn(screen As Screen, fullscreen As Boolean)
        _layout.ShowOn(screen, fullscreen)
    End Sub

    Public Sub ApplyAppearance(appearance As LoadedAppearance)
        Dim style = appearance.Profile.Big
        _layout.ApplyAppearance(appearance.Picture(style.Background, My.Resources._26_big), Color.FromArgb(style.TextArgb))
    End Sub

    Public Sub Render(state As BigGameState)
        TotalTxt.Visible = True
        TotalLbl.Visible = True
        RoundScoreLbl.Visible = True
        TotalLbl.Text = state.Total.ToString()
        RoundScoreLbl.Text = state.Total.ToString()
        Dim captionBounds = _totalCaptionLeft
        Dim valueBounds = _totalValueLeft
        ' Первый опубликованный текст второго участника переносит общий итог.
        ' Черновик, маска и таймер этого не делают. Положение выводится из снимка:
        ' Undo, сброс и восстановление работают без нового флага в сохранении.
        If state.Answers.Skip(5).Any(Function(answer) answer.TextVisible) Then
            captionBounds.Offset(_totalRightOffset, 0)
            valueBounds.Offset(_totalRightOffset, 0)
        End If
        _layout.SetDesignBounds(TotalTxt, captionBounds)
        _layout.SetDesignBounds(TotalLbl, valueBounds)
        For i = 0 To 9
            Dim answer = state.Answers(i)
            Dim masked = i < 5 AndAlso state.HideFirstPlayer
            ' Маска меняет вывод, но не данные и стадию раскрытия.
            ' Непоказанные тексты/цены не публикуются при скрытии первого участника.
            _texts(i).Visible = answer.TextVisible
            _scores(i).Visible = answer.ScoreVisible
            _texts(i).Text = If(masked, "••••••••", answer.Text)
            _scores(i).Text = If(masked, "••", answer.Points.ToString())
        Next
        _layout.RefreshText()
    End Sub

    Public Sub RenderTimer(seconds As Integer, visible As Boolean)
        ' Отсчётом и его звуком владеет Countdown; табло получает готовую цифру.
        TimerLbl.Text = seconds.ToString()
        TimerLbl.Visible = visible
        _layout.RefreshText()
    End Sub
End Class
