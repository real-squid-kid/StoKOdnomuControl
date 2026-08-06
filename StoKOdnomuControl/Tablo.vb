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
        LeftStrike1Lbl.Image = My.Resources._26_chance_l
        LeftStrike2Lbl.Image = My.Resources._26_chance_l
        LeftStrike3Lbl.Image = My.Resources._26_chance_l
        RightStrike1Lbl.Image = My.Resources._26_chance_r
        RightStrike2Lbl.Image = My.Resources._26_chance_r
        RightStrike3Lbl.Image = My.Resources._26_chance_r
    End Sub

    Public Sub Prepare()
        'hide strikes
        ScoreLeftLbl.Visible = True
        ScoreRightLbl.Visible = True
        RoundScoreLbl.Visible = True
        RoundScoreLbl.Text = "0"
        LeftStrike1Lbl.Image = My.Resources._26_chance_l
        LeftStrike2Lbl.Image = My.Resources._26_chance_l
        LeftStrike3Lbl.Image = My.Resources._26_chance_l
        RightStrike1Lbl.Image = My.Resources._26_chance_r
        RightStrike2Lbl.Image = My.Resources._26_chance_r
        RightStrike3Lbl.Image = My.Resources._26_chance_r
        'hide answers
        Answer1Lbl.Text = "1."
        Answer2Lbl.Text = "2."
        Answer3Lbl.TextAlign = ContentAlignment.MiddleLeft
        Answer4Lbl.TextAlign = ContentAlignment.MiddleLeft
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
                        My.Computer.Audio.Play(My.Resources._26_open, AudioPlayMode.Background)
                    Case 1
                        My.Computer.Audio.Play(My.Resources._26_noscore, AudioPlayMode.Background)
                End Select
            Case 2
                Answer2Lbl.Text = "2. " & text
                Answer2ScoreLbl.Text = value
                RoundScoreLbl.Text = totalscr
                Select Case sound
                    Case 2
                        My.Computer.Audio.Play(My.Resources._26_open, AudioPlayMode.Background)
                    Case 1
                        My.Computer.Audio.Play(My.Resources._26_noscore, AudioPlayMode.Background)
                End Select
            Case 3
                Answer3Lbl.Text = "3. " & text
                Answer3ScoreLbl.Text = value
                RoundScoreLbl.Text = totalscr
                Select Case sound
                    Case 2
                        My.Computer.Audio.Play(My.Resources._26_open, AudioPlayMode.Background)
                    Case 1
                        My.Computer.Audio.Play(My.Resources._26_noscore, AudioPlayMode.Background)
                End Select
            Case 4
                Answer4Lbl.Text = "4. " & text
                Answer4ScoreLbl.Text = value
                RoundScoreLbl.Text = totalscr
                Select Case sound
                    Case 2
                        My.Computer.Audio.Play(My.Resources._26_open, AudioPlayMode.Background)
                    Case 1
                        My.Computer.Audio.Play(My.Resources._26_noscore, AudioPlayMode.Background)
                End Select
            Case 5
                Answer5Lbl.Text = "5. " & text
                Answer5ScoreLbl.Text = value
                RoundScoreLbl.Text = totalscr
                Select Case sound
                    Case 2
                        My.Computer.Audio.Play(My.Resources._26_open, AudioPlayMode.Background)
                    Case 1
                        My.Computer.Audio.Play(My.Resources._26_noscore, AudioPlayMode.Background)
                End Select
            Case 6
                Answer6Lbl.Text = "6. " & text
                Answer6ScoreLbl.Text = value
                RoundScoreLbl.Text = totalscr
                Select Case sound
                    Case 2
                        My.Computer.Audio.Play(My.Resources._26_open, AudioPlayMode.Background)
                    Case 1
                        My.Computer.Audio.Play(My.Resources._26_noscore, AudioPlayMode.Background)
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
                            LeftStrike1Lbl.Image = My.Resources._26_wrong_l
                            My.Computer.Audio.Play(My.Resources._26_wrong, AudioPlayMode.Background)
                        Else
                            LeftStrike1Lbl.Image = My.Resources._26_chance_l
                        End If
                    Case 2
                        If show = True Then
                            LeftStrike2Lbl.Image = My.Resources._26_wrong_l
                            My.Computer.Audio.Play(My.Resources._26_wrong, AudioPlayMode.Background)
                        Else
                            LeftStrike2Lbl.Image = My.Resources._26_chance_l
                        End If
                    Case 3
                        If show = True Then
                            LeftStrike3Lbl.Image = My.Resources._26_wrong_l
                            My.Computer.Audio.Play(My.Resources._26_wrong, AudioPlayMode.Background)
                        Else
                            LeftStrike3Lbl.Image = My.Resources._26_chance_l
                        End If
                End Select
            Case "R"
                Select Case place
                    Case 1
                        If show = True Then
                            RightStrike1Lbl.Image = My.Resources._26_wrong_r
                            My.Computer.Audio.Play(My.Resources._26_wrong, AudioPlayMode.Background)
                        Else
                            RightStrike1Lbl.Image = My.Resources._26_chance_r
                        End If
                    Case 2
                        If show = True Then
                            RightStrike2Lbl.Image = My.Resources._26_wrong_r
                            My.Computer.Audio.Play(My.Resources._26_wrong, AudioPlayMode.Background)
                        Else
                            RightStrike2Lbl.Image = My.Resources._26_chance_r
                        End If
                    Case 3
                        If show = True Then
                            RightStrike3Lbl.Image = My.Resources._26_wrong_r
                            My.Computer.Audio.Play(My.Resources._26_wrong, AudioPlayMode.Background)
                        Else
                            RightStrike3Lbl.Image = My.Resources._26_chance_r
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

    Public Sub SetRound(e As Integer)
        Select Case e
            Case 1
                LeftRoundLbl.Text = "1"
                RightRoundLbl.Text = "1"
            Case 2
                LeftRoundLbl.Text = "x2"
                RightRoundLbl.Text = "x2"
            Case 3
                LeftRoundLbl.Text = "x3"
                RightRoundLbl.Text = "x3"
            Case 4
                LeftRoundLbl.Text = "?"
                RightRoundLbl.Text = "?"
        End Select
    End Sub

    Public Sub AttractMode(e As Integer)
        Me.Prepare()
        Answer5Lbl.Text = ""
        Answer6Lbl.Text = ""
        Answer1Lbl.Text = ""
        Answer2Lbl.Text = ""
        Answer3Lbl.TextAlign = ContentAlignment.MiddleCenter
        Answer4Lbl.TextAlign = ContentAlignment.MiddleCenter

        Select Case e
            Case 0
                Answer3Lbl.Text = "СТО"
                Answer4Lbl.Text = "К ОДНОМУ"
            Case 1
                Answer3Lbl.Text = "ПРОСТАЯ"
                Answer4Lbl.Text = "ИГРА"
            Case 2
                Answer3Lbl.Text = "ДВОЙНАЯ"
                Answer4Lbl.Text = "ИГРА"
            Case 3
                Answer3Lbl.Text = "ТРОЙНАЯ"
                Answer4Lbl.Text = "ИГРА"
            Case 4
                Answer3Lbl.Text = "ИГРА"
                Answer4Lbl.Text = "НАОБОРОТ"
            Case 5
                Answer3Lbl.Text = "БОЛЬШАЯ"
                Answer4Lbl.Text = "ИГРА"
        End Select
    End Sub
End Class