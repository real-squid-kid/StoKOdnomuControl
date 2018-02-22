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

    Public Sub SetTimer(e As Integer)
        TimerLbl.Text = e
        TimerLbl.Visible = True
        My.Computer.Audio.Play(My.Resources.prosto_open, AudioPlayMode.Background)
    End Sub

    Public Sub StartTimer()
        If Timer1.Enabled = False Then
            Timer1.Enabled = True
        Else
            StopTimer()
        End If

    End Sub

    Public Sub StopTimer()
        TimerLbl.Visible = False
        Timer1.Enabled = False
    End Sub

    Private Sub Timer1_Tick(sender As Object, e As EventArgs) Handles Timer1.Tick
        TimerLbl.Text -= 1
        If TimerLbl.Text < 0 Then
            StopTimer()
            My.Computer.Audio.Play(My.Resources.prosto_wrong, AudioPlayMode.Background)
        End If
    End Sub

    Public Sub Reveal(e As String, param As String)
        Select Case e
            Case 1
                Answer1Lbl.Text = param
                Answer1Lbl.Visible = True
            Case "1A"
                Answer1ScoreLbl.Text = param
                Answer1ScoreLbl.Visible = True
                TotalLbl.Text = BigGameControl.ScoreTxt.Text
                RoundScoreLbl.Text = TotalLbl.Text
                If param = 0 Then
                    My.Computer.Audio.Play(My.Resources.prosto_wrong, AudioPlayMode.Background)
                Else
                    My.Computer.Audio.Play(My.Resources.prosto_open, AudioPlayMode.Background)
                End If
            Case 2
                Answer2Lbl.Text = param
                Answer2Lbl.Visible = True
            Case "2A"
                Answer2ScoreLbl.Text = param
                Answer2ScoreLbl.Visible = True
                TotalLbl.Text = BigGameControl.ScoreTxt.Text
                RoundScoreLbl.Text = TotalLbl.Text
                If param = 0 Then
                    My.Computer.Audio.Play(My.Resources.prosto_wrong, AudioPlayMode.Background)
                Else
                    My.Computer.Audio.Play(My.Resources.prosto_open, AudioPlayMode.Background)
                End If
            Case 3
                Answer3Lbl.Text = param
                Answer3Lbl.Visible = True
            Case "3A"
                Answer3ScoreLbl.Text = param
                Answer3ScoreLbl.Visible = True
                TotalLbl.Text = BigGameControl.ScoreTxt.Text
                RoundScoreLbl.Text = TotalLbl.Text
                If param = 0 Then
                    My.Computer.Audio.Play(My.Resources.prosto_wrong, AudioPlayMode.Background)
                Else
                    My.Computer.Audio.Play(My.Resources.prosto_open, AudioPlayMode.Background)
                End If
            Case 4
                Answer4Lbl.Text = param
                Answer4Lbl.Visible = True
            Case "4A"
                Answer4ScoreLbl.Text = param
                Answer4ScoreLbl.Visible = True
                TotalLbl.Text = BigGameControl.ScoreTxt.Text
                RoundScoreLbl.Text = TotalLbl.Text
                If param = 0 Then
                    My.Computer.Audio.Play(My.Resources.prosto_wrong, AudioPlayMode.Background)
                Else
                    My.Computer.Audio.Play(My.Resources.prosto_open, AudioPlayMode.Background)
                End If
            Case 5
                Answer5Lbl.Text = param
                Answer5Lbl.Visible = True
            Case "5A"
                Answer5ScoreLbl.Text = param
                Answer5ScoreLbl.Visible = True
                TotalLbl.Text = BigGameControl.ScoreTxt.Text
                RoundScoreLbl.Text = TotalLbl.Text
                If param = 0 Then
                    My.Computer.Audio.Play(My.Resources.prosto_wrong, AudioPlayMode.Background)
                Else
                    My.Computer.Audio.Play(My.Resources.prosto_open, AudioPlayMode.Background)
                End If
            Case 6
                Answer6Lbl.Text = param
                Answer6Lbl.Visible = True
            Case "6A"
                Answer6ScoreLbl.Text = param
                Answer6ScoreLbl.Visible = True
                TotalLbl.Text = BigGameControl.ScoreTxt.Text
                RoundScoreLbl.Text = TotalLbl.Text
                If param = 0 Then
                    My.Computer.Audio.Play(My.Resources.prosto_wrong, AudioPlayMode.Background)
                Else
                    My.Computer.Audio.Play(My.Resources.prosto_open, AudioPlayMode.Background)
                End If
            Case 7
                Answer7Lbl.Text = param
                Answer7Lbl.Visible = True
            Case "7A"
                Answer7ScoreLbl.Text = param
                Answer7ScoreLbl.Visible = True
                TotalLbl.Text = BigGameControl.ScoreTxt.Text
                RoundScoreLbl.Text = TotalLbl.Text
                If param = 0 Then
                    My.Computer.Audio.Play(My.Resources.prosto_wrong, AudioPlayMode.Background)
                Else
                    My.Computer.Audio.Play(My.Resources.prosto_open, AudioPlayMode.Background)
                End If
            Case 8
                Answer8Lbl.Text = param
                Answer8Lbl.Visible = True
            Case "8A"
                Answer8ScoreLbl.Text = param
                Answer8ScoreLbl.Visible = True
                TotalLbl.Text = BigGameControl.ScoreTxt.Text
                RoundScoreLbl.Text = TotalLbl.Text
                If param = 0 Then
                    My.Computer.Audio.Play(My.Resources.prosto_wrong, AudioPlayMode.Background)
                Else
                    My.Computer.Audio.Play(My.Resources.prosto_open, AudioPlayMode.Background)
                End If
            Case 9
                Answer9Lbl.Text = param
                Answer9Lbl.Visible = True
            Case "9A"
                Answer9ScoreLbl.Text = param
                Answer9ScoreLbl.Visible = True
                TotalLbl.Text = BigGameControl.ScoreTxt.Text
                RoundScoreLbl.Text = TotalLbl.Text
                If param = 0 Then
                    My.Computer.Audio.Play(My.Resources.prosto_wrong, AudioPlayMode.Background)
                Else
                    My.Computer.Audio.Play(My.Resources.prosto_open, AudioPlayMode.Background)
                End If
            Case 10
                Answer10Lbl.Text = param
                Answer10Lbl.Visible = True
            Case "10A"
                Answer10ScoreLbl.Text = param
                Answer10ScoreLbl.Visible = True
                TotalLbl.Text = BigGameControl.ScoreTxt.Text
                RoundScoreLbl.Text = TotalLbl.Text
                If param = 0 Then
                    My.Computer.Audio.Play(My.Resources.prosto_wrong, AudioPlayMode.Background)
                Else
                    My.Computer.Audio.Play(My.Resources.prosto_open, AudioPlayMode.Background)
                End If
        End Select
    End Sub
End Class