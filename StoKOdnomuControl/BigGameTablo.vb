Public Class BigGameTablo
    Private Sub BigGameTablo_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        'hide all
        TotalTxt.Visible = False
        TotalLbl.Visible = False
        RoundScoreLbl.Visible = False
        Answer1Lbl.Visible = False
        Answer2Lbl.Visible = False
        Answer3Lbl.Visible = False
        Answer4Lbl.Visible = False
        Answer5Lbl.Visible = False
        Answer6Lbl.Visible = False
        Answer7Lbl.Visible = False
        Answer8Lbl.Visible = False
        Answer9Lbl.Visible = False
        Answer10Lbl.Visible = False
        Answer1ScoreLbl.Visible = False
        Answer2ScoreLbl.Visible = False
        Answer3ScoreLbl.Visible = False
        Answer4ScoreLbl.Visible = False
        Answer5ScoreLbl.Visible = False
        Answer6ScoreLbl.Visible = False
        Answer7ScoreLbl.Visible = False
        Answer8ScoreLbl.Visible = False
        Answer9ScoreLbl.Visible = False
        Answer10ScoreLbl.Visible = False
        TimerLbl.Visible = False
    End Sub

    Public Sub Prepare()
        TotalLbl.Visible = True
        TotalTxt.Visible = True
        RoundScoreLbl.Visible = True
    End Sub

    Public Sub HideAnswers()
        Answer1Lbl.Text = "••••••••"
        Answer1ScoreLbl.Text = "••"
        Answer2Lbl.Text = "••••••••"
        Answer2ScoreLbl.Text = "••"
        Answer3Lbl.Text = "••••••••"
        Answer3ScoreLbl.Text = "••"
        Answer4Lbl.Text = "••••••••"
        Answer4ScoreLbl.Text = "••"
        Answer5Lbl.Text = "••••••••"
        Answer5ScoreLbl.Text = "••"
    End Sub

    Public Sub ShowAnswers()
        Answer1Lbl.Text = BigGameControl.Answer1Txt.Text
        Answer1ScoreLbl.Text = BigGameControl.Answer1CostTxt.Text
        Answer2Lbl.Text = BigGameControl.Answer2Txt.Text
        Answer2ScoreLbl.Text = BigGameControl.Answer2CostTxt.Text
        Answer3Lbl.Text = BigGameControl.Answer3Txt.Text
        Answer3ScoreLbl.Text = BigGameControl.Answer3CostTxt.Text
        Answer4Lbl.Text = BigGameControl.Answer4Txt.Text
        Answer4ScoreLbl.Text = BigGameControl.Answer4CostTxt.Text
        Answer5Lbl.Text = BigGameControl.Answer5Txt.Text
        Answer5ScoreLbl.Text = BigGameControl.Answer5CostTxt.Text
    End Sub
End Class