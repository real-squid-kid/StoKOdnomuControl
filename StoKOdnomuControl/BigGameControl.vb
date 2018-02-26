Public Class BigGameControl
    Dim State As Integer = 0

    Private Sub GrayChk_CheckedChanged(sender As Object, e As EventArgs) Handles GrayChk.CheckedChanged
        If GrayChk.Checked = True Then
            ScoreTxt.ForeColor = Color.LightGray
            Answer1Txt.ForeColor = Color.LightGray
            Answer1CostTxt.ForeColor = Color.LightGray
            Answer2Txt.ForeColor = Color.LightGray
            Answer2CostTxt.ForeColor = Color.LightGray
            Answer3Txt.ForeColor = Color.LightGray
            Answer3CostTxt.ForeColor = Color.LightGray
            Answer4Txt.ForeColor = Color.LightGray
            Answer4CostTxt.ForeColor = Color.LightGray
            Answer5Txt.ForeColor = Color.LightGray
            Answer5CostTxt.ForeColor = Color.LightGray
            Answer6Txt.ForeColor = Color.LightGray
            Answer6CostTxt.ForeColor = Color.LightGray
            Answer7Txt.ForeColor = Color.LightGray
            Answer7CostTxt.ForeColor = Color.LightGray
            Answer8Txt.ForeColor = Color.LightGray
            Answer8CostTxt.ForeColor = Color.LightGray
            Answer9Txt.ForeColor = Color.LightGray
            Answer9CostTxt.ForeColor = Color.LightGray
            Answer10Txt.ForeColor = Color.LightGray
            Answer10CostTxt.ForeColor = Color.LightGray
        Else
            ScoreTxt.ForeColor = Color.Black
            Answer1Txt.ForeColor = Color.Black
            Answer1CostTxt.ForeColor = Color.Black
            Answer2Txt.ForeColor = Color.Black
            Answer2CostTxt.ForeColor = Color.Black
            Answer3Txt.ForeColor = Color.Black
            Answer3CostTxt.ForeColor = Color.Black
            Answer4Txt.ForeColor = Color.Black
            Answer4CostTxt.ForeColor = Color.Black
            Answer5Txt.ForeColor = Color.Black
            Answer5CostTxt.ForeColor = Color.Black
            Answer6Txt.ForeColor = Color.Black
            Answer6CostTxt.ForeColor = Color.Black
            Answer7Txt.ForeColor = Color.Black
            Answer7CostTxt.ForeColor = Color.Black
            Answer8Txt.ForeColor = Color.Black
            Answer8CostTxt.ForeColor = Color.Black
            Answer9Txt.ForeColor = Color.Black
            Answer9CostTxt.ForeColor = Color.Black
            Answer10Txt.ForeColor = Color.Black
            Answer10CostTxt.ForeColor = Color.Black
        End If
    End Sub

    Private Sub ShowInfoBtn_Click(sender As Object, e As EventArgs) Handles ShowInfoBtn.Click
        BigGameTablo.Prepare()
    End Sub

    Private Sub SameAnswerBtn_Click(sender As Object, e As EventArgs) Handles SameAnswerBtn.Click
        My.Computer.Audio.Play(My.Resources.sto_dulpicate, AudioPlayMode.Background)
    End Sub

    Private Sub Timer15Btn_Click(sender As Object, e As EventArgs) Handles Timer15Btn.Click
        BigGameTablo.SetTimer(15)
    End Sub

    Private Sub Timer20Btn_Click(sender As Object, e As EventArgs) Handles Timer20Btn.Click
        BigGameTablo.SetTimer(20)
    End Sub

    Private Sub StartStopBtn_Click(sender As Object, e As EventArgs) Handles StartStopBtn.Click
        BigGameTablo.StartTimer()
    End Sub

    Private Sub HideAnswersChk_CheckedChanged(sender As Object, e As EventArgs) Handles HideAnswersChk.CheckedChanged
        If HideAnswersChk.Checked = True Then
            BigGameTablo.HideAnswers()
        Else
            BigGameTablo.ShowAnswers()
        End If
    End Sub

    Private Sub NextBtn_Click(sender As Object, e As EventArgs) Handles NextBtn.Click
        State += 1
        If State < 21 Then
            UpdateState(State)
        End If
    End Sub

    Private Sub UpdateState(e As Integer)
        Select Case e
            Case 1
                BigGameTablo.Reveal(1, Answer1Txt.Text)
            Case 2
                ScoreTxt.Text += Int(Answer1CostTxt.Text)
                BigGameTablo.Reveal("1A", Answer1CostTxt.Text)
            Case 3
                BigGameTablo.Reveal(2, Answer2Txt.Text)
            Case 4
                ScoreTxt.Text += Int(Answer2CostTxt.Text)
                BigGameTablo.Reveal("2A", Answer2CostTxt.Text)
            Case 5
                BigGameTablo.Reveal(3, Answer3Txt.Text)
            Case 6
                ScoreTxt.Text += Int(Answer3CostTxt.Text)
                BigGameTablo.Reveal("3A", Answer3CostTxt.Text)
            Case 7
                BigGameTablo.Reveal(4, Answer4Txt.Text)
            Case 8
                ScoreTxt.Text += Int(Answer4CostTxt.Text)
                BigGameTablo.Reveal("4A", Answer4CostTxt.Text)
            Case 9
                BigGameTablo.Reveal(5, Answer5Txt.Text)
            Case 10
                ScoreTxt.Text += Int(Answer5CostTxt.Text)
                BigGameTablo.Reveal("5A", Answer5CostTxt.Text)
            Case 11
                BigGameTablo.Reveal(6, Answer6Txt.Text)
            Case 12
                ScoreTxt.Text += Int(Answer6CostTxt.Text)
                BigGameTablo.Reveal("6A", Answer6CostTxt.Text)
            Case 13
                BigGameTablo.Reveal(7, Answer7Txt.Text)
            Case 14
                ScoreTxt.Text += Int(Answer7CostTxt.Text)
                BigGameTablo.Reveal("7A", Answer7CostTxt.Text)
            Case 15
                BigGameTablo.Reveal(8, Answer8Txt.Text)
            Case 16
                ScoreTxt.Text += Int(Answer8CostTxt.Text)
                BigGameTablo.Reveal("8A", Answer8CostTxt.Text)
            Case 17
                BigGameTablo.Reveal(9, Answer9Txt.Text)
            Case 18
                ScoreTxt.Text += Int(Answer9CostTxt.Text)
                BigGameTablo.Reveal("9A", Answer9CostTxt.Text)
            Case 19
                BigGameTablo.Reveal(10, Answer10Txt.Text)
            Case 20
                ScoreTxt.Text += Int(Answer10CostTxt.Text)
                BigGameTablo.Reveal("10A", Answer10CostTxt.Text)

        End Select
    End Sub
End Class