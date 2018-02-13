Public Class Tablo
    Dim Player As New AxWMPLib.AxWindowsMediaPlayer
    Private Sub Tablo_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        'hide all
        ScoreLeftLbl.Visible = False
        ScoreRightLbl.Visible = False
        RoundScoreLbl.Visible = False
        Answer1Lbl.Visible = False
        Answer2Lbl.Visible = False
        Answer3Lbl.Visible = False
        Answer4Lbl.Visible = False
        Answer5Lbl.Visible = False
        Answer6Lbl.Visible = False
        Answer1ScoreLbl.Visible = False
        Answer2ScoreLbl.Visible = False
        Answer3ScoreLbl.Visible = False
        Answer4ScoreLbl.Visible = False
        Answer5ScoreLbl.Visible = False
        Answer6ScoreLbl.Visible = False
        LeftStrike1Lbl.Visible = False
        LeftStrike2Lbl.Visible = False
        LeftStrike3Lbl.Visible = False
        RightStrike1Lbl.Visible = False
        RightStrike2Lbl.Visible = False
        RightStrike3Lbl.Visible = False
    End Sub

    Public Sub Prepare()
        'hide strikes
        ScoreLeftLbl.Visible = True
        ScoreRightLbl.Visible = True
        RoundScoreLbl.Visible = True
        RoundScoreLbl.Text = "0"
        LeftStrike1Lbl.Visible = False
        LeftStrike2Lbl.Visible = False
        LeftStrike3Lbl.Visible = False
        RightStrike1Lbl.Visible = False
        RightStrike2Lbl.Visible = False
        RightStrike3Lbl.Visible = False
        'hide answers
        Answer1Lbl.Text = "1."
        Answer2Lbl.Text = "2."
        Answer3Lbl.Text = "3."
        Answer4Lbl.Text = "4."
        Answer5Lbl.Text = "5."
        Answer6Lbl.Text = "6."
        Answer1ScoreLbl.Text = ""
        Answer2ScoreLbl.Text = ""
        Answer3ScoreLbl.Text = ""
        Answer4ScoreLbl.Text = ""
        Answer5ScoreLbl.Text = ""
        Answer6ScoreLbl.Text = ""
        Answer1Lbl.Visible = True
        Answer2Lbl.Visible = True
        Answer3Lbl.Visible = True
        Answer4Lbl.Visible = True
        Answer5Lbl.Visible = True
        Answer6Lbl.Visible = True
        Answer1ScoreLbl.Visible = True
        Answer2ScoreLbl.Visible = True
        Answer3ScoreLbl.Visible = True
        Answer4ScoreLbl.Visible = True
        Answer5ScoreLbl.Visible = True
        Answer6ScoreLbl.Visible = True
    End Sub

    Public Sub Reveal(pos As Integer, text As String, value As Integer, totalscr As String, sound As Integer)
        Select Case pos
            Case 1
                Answer1Lbl.Text = "1. " & text
                Answer1ScoreLbl.Text = value
                RoundScoreLbl.Text = totalscr
                Select Case sound
                    Case 2
                        My.Computer.Audio.Play(My.Resources.line_open, AudioPlayMode.Background)
                    Case 1
                        My.Computer.Audio.Play(My.Resources.line_wrong, AudioPlayMode.Background)
                End Select
            Case 2
                Answer2Lbl.Text = "2. " & text
                Answer2ScoreLbl.Text = value
                RoundScoreLbl.Text = totalscr
                Select Case sound
                    Case 2
                        My.Computer.Audio.Play(My.Resources.line_open, AudioPlayMode.Background)
                    Case 1
                        My.Computer.Audio.Play(My.Resources.line_wrong, AudioPlayMode.Background)
                End Select
            Case 3
                Answer3Lbl.Text = "3. " & text
                Answer3ScoreLbl.Text = value
                RoundScoreLbl.Text = totalscr
                Select Case sound
                    Case 2
                        My.Computer.Audio.Play(My.Resources.line_open, AudioPlayMode.Background)
                    Case 1
                        My.Computer.Audio.Play(My.Resources.line_wrong, AudioPlayMode.Background)
                End Select
            Case 4
                Answer4Lbl.Text = "4. " & text
                Answer4ScoreLbl.Text = value
                RoundScoreLbl.Text = totalscr
                Select Case sound
                    Case 2
                        My.Computer.Audio.Play(My.Resources.line_open, AudioPlayMode.Background)
                    Case 1
                        My.Computer.Audio.Play(My.Resources.line_wrong, AudioPlayMode.Background)
                End Select
            Case 5
                Answer5Lbl.Text = "5. " & text
                Answer5ScoreLbl.Text = value
                RoundScoreLbl.Text = totalscr
                Select Case sound
                    Case 2
                        My.Computer.Audio.Play(My.Resources.line_open, AudioPlayMode.Background)
                    Case 1
                        My.Computer.Audio.Play(My.Resources.line_wrong, AudioPlayMode.Background)
                End Select
            Case 6
                Answer6Lbl.Text = "6. " & text
                Answer6ScoreLbl.Text = value
                RoundScoreLbl.Text = totalscr
                Select Case sound
                    Case 2
                        My.Computer.Audio.Play(My.Resources.line_open, AudioPlayMode.Background)
                    Case 1
                        My.Computer.Audio.Play(My.Resources.line_wrong, AudioPlayMode.Background)
                End Select
        End Select
    End Sub

    Public Sub RollBack(pos As Integer, totalscr As Integer)
        Select Case pos
            Case 1
                Answer1Lbl.Text = "1."
                Answer1ScoreLbl.Text = ""
                RoundScoreLbl.Text = totalscr
            Case 2
                Answer2Lbl.Text = "2."
                Answer2ScoreLbl.Text = ""
                RoundScoreLbl.Text = totalscr
            Case 3
                Answer3Lbl.Text = "3."
                Answer3ScoreLbl.Text = ""
                RoundScoreLbl.Text = totalscr
            Case 4
                Answer4Lbl.Text = "4."
                Answer4ScoreLbl.Text = ""
                RoundScoreLbl.Text = totalscr
            Case 5
                Answer5Lbl.Text = "5."
                Answer5ScoreLbl.Text = ""
                RoundScoreLbl.Text = totalscr
            Case 6
                Answer6Lbl.Text = "6."
                Answer6ScoreLbl.Text = ""
                RoundScoreLbl.Text = totalscr
        End Select
    End Sub

    Public Sub Strike(pos As Char, place As Integer, show As Boolean)
        Select Case pos
            Case "L"
                Select Case place
                    Case 1
                        If show = True Then
                            LeftStrike1Lbl.Visible = True
                            My.Computer.Audio.Play(My.Resources.wrong, AudioPlayMode.Background)
                        Else
                            LeftStrike1Lbl.Visible = False
                        End If
                    Case 2
                        If show = True Then
                            LeftStrike2Lbl.Visible = True
                            My.Computer.Audio.Play(My.Resources.wrong, AudioPlayMode.Background)
                        Else
                            LeftStrike2Lbl.Visible = False
                        End If
                    Case 3
                        If show = True Then
                            LeftStrike3Lbl.Visible = True
                            My.Computer.Audio.Play(My.Resources.wrong, AudioPlayMode.Background)
                        Else
                            LeftStrike3Lbl.Visible = False
                        End If
                End Select
            Case "R"
                Select Case place
                    Case 1
                        If show = True Then
                            RightStrike1Lbl.Visible = True
                            My.Computer.Audio.Play(My.Resources.wrong, AudioPlayMode.Background)
                        Else
                            RightStrike1Lbl.Visible = False
                        End If
                    Case 2
                        If show = True Then
                            RightStrike2Lbl.Visible = True
                            My.Computer.Audio.Play(My.Resources.wrong, AudioPlayMode.Background)
                        Else
                            RightStrike2Lbl.Visible = False
                        End If
                    Case 3
                        If show = True Then
                            RightStrike3Lbl.Visible = True
                            My.Computer.Audio.Play(My.Resources.wrong, AudioPlayMode.Background)
                        Else
                            RightStrike3Lbl.Visible = False
                        End If
                End Select
        End Select
    End Sub

    Public Sub UpdateScore(pos As Char, value As Integer, sound As Boolean)
        Select Case pos
            Case "L"
                ScoreLeftLbl.Text = value
                If sound = True Then My.Computer.Audio.Play(My.Resources.bing, AudioPlayMode.Background)
            Case "R"
                ScoreRightLbl.Text = value
                If sound = True Then My.Computer.Audio.Play(My.Resources.bing, AudioPlayMode.Background)
            Case "C"
                RoundScoreLbl.Text = value
        End Select
    End Sub
End Class