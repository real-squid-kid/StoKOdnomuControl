Imports System.IO
Public Class Form1
    Dim Answers As New List(Of String)
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        LeftCross1Btn.BackColor = Color.LightGray
        LeftCross2Btn.BackColor = Color.LightGray
        LeftCross3Btn.BackColor = Color.LightGray
        RightCross1Btn.BackColor = Color.LightGray
        RightCross2Btn.BackColor = Color.LightGray
        RightCross3Btn.BackColor = Color.LightGray
    End Sub

    Private Sub OpenQuestionsBtn_Click(sender As Object, e As EventArgs) Handles OpenQuestionsBtn.Click
        Try
            Answers.Clear()
            Dim Openfile As New OpenFileDialog
            Dim Result As String
            If Openfile.ShowDialog() = System.Windows.Forms.DialogResult.OK Then
                Result = File.ReadAllText(Openfile.FileName)
                Dim a() As String
                a = Result.Split(ControlChars.CrLf.ToCharArray, StringSplitOptions.RemoveEmptyEntries)
                For Each b In a
                    Answers.Add(b)
                Next
                If Answers.Count <> 24 Then
                    Throw New Exception("Incompatible base format.")
                End If
            Else
            End If
        Catch exc As Exception
            MessageBox.Show("Ошибка: " & exc.Message & " Данные не были внесены.", "Невозможно открыть файл", MessageBoxButtons.OK, MessageBoxIcon.Stop)
        Answers.Clear()
        End Try
    End Sub

    Private Sub ShowTabloBtn_Click(sender As Object, e As EventArgs) Handles ShowTabloBtn.Click
        Tablo.Show()

    End Sub

    Private Sub LeftCross3Btn_Click(sender As Object, e As EventArgs) Handles LeftCross3Btn.Click
        If LeftCross3Btn.BackColor = Color.LightGray Then
            LeftCross3Btn.BackColor = Color.Red
            Tablo.Strike("L", 3, True)
        Else
            LeftCross3Btn.BackColor = Color.LightGray
            Tablo.Strike("L", 3, False)
        End If
    End Sub

    Private Sub LeftCross2Btn_Click(sender As Object, e As EventArgs) Handles LeftCross2Btn.Click
        If LeftCross2Btn.BackColor = Color.LightGray Then
            LeftCross2Btn.BackColor = Color.Red
            Tablo.Strike("L", 2, True)
        Else
            LeftCross2Btn.BackColor = Color.LightGray
            Tablo.Strike("L", 2, False)
        End If
    End Sub

    Private Sub LeftCross1Btn_Click(sender As Object, e As EventArgs) Handles LeftCross1Btn.Click
        If LeftCross1Btn.BackColor = Color.LightGray Then
            LeftCross1Btn.BackColor = Color.Red
            Tablo.Strike("L", 1, True)
        Else
            LeftCross1Btn.BackColor = Color.LightGray
            Tablo.Strike("L", 1, False)
        End If
    End Sub

    Private Sub RightCross3Btn_Click(sender As Object, e As EventArgs) Handles RightCross3Btn.Click
        If RightCross3Btn.BackColor = Color.LightGray Then
            RightCross3Btn.BackColor = Color.Red
            Tablo.Strike("R", 3, True)
        Else
            RightCross3Btn.BackColor = Color.LightGray
            Tablo.Strike("R", 3, False)
        End If
    End Sub

    Private Sub RightCross2Btn_Click(sender As Object, e As EventArgs) Handles RightCross2Btn.Click
        If RightCross2Btn.BackColor = Color.LightGray Then
            RightCross2Btn.BackColor = Color.Red
            Tablo.Strike("R", 2, True)
        Else
            RightCross2Btn.BackColor = Color.LightGray
            Tablo.Strike("R", 2, False)
        End If
    End Sub

    Private Sub RightCross1Btn_Click(sender As Object, e As EventArgs) Handles RightCross1Btn.Click
        If RightCross1Btn.BackColor = Color.LightGray Then
            RightCross1Btn.BackColor = Color.Red
            Tablo.Strike("R", 1, True)
        Else
            RightCross1Btn.BackColor = Color.LightGray
            Tablo.Strike("R", 1, False)
        End If
    End Sub

    Private Sub ScoreLeftRefreshTxt_Click(sender As Object, e As EventArgs) Handles ScoreLeftRefreshTxt.Click
        Tablo.UpdateScore("R", ScoreLeftTxt.Text, False)
    End Sub

    Private Sub ScoreRightRefreshTxt_Click(sender As Object, e As EventArgs) Handles ScoreRightRefreshTxt.Click
        Tablo.UpdateScore("L", ScoreRightTxt.Text, False)
    End Sub

    Private Sub RoundScoreRefreshBtn_Click(sender As Object, e As EventArgs) Handles RoundScoreRefreshBtn.Click
        Tablo.UpdateScore("C", RoundScoreTxt.Text, False)
    End Sub

    Private Sub TransferLeftBtn_Click(sender As Object, e As EventArgs) Handles TransferLeftBtn.Click
        Dim a As Integer
        a = Int(ScoreLeftTxt.Text) + Int(RoundScoreTxt.Text)
        ScoreLeftTxt.Text = a
        RoundScoreTxt.Text = 0
        Tablo.UpdateScore("L", ScoreLeftTxt.Text, True)
        Tablo.UpdateScore("C", RoundScoreTxt.Text, False)
    End Sub

    Private Sub TransferRightBtn_Click(sender As Object, e As EventArgs) Handles TransferRightBtn.Click
        Dim a As Integer
        a = Int(ScoreRightTxt.Text) + Int(RoundScoreTxt.Text)
        ScoreRightTxt.Text = a
        RoundScoreTxt.Text = 0
        Tablo.UpdateScore("R", ScoreRightTxt.Text, True)
        Tablo.UpdateScore("C", RoundScoreTxt.Text, False)
    End Sub

    Private Sub Answer1RevealBtn_Click(sender As Object, e As EventArgs) Handles Answer1RevealBtn.Click
        If Answer1RevealBtn.BackColor <> Color.LightGreen Then
            If AddUpChk.Checked = True Then
                Dim a As Integer
                a = Int(RoundScoreTxt.Text) + Int(Answer1CostTxt.Text)
                RoundScoreTxt.Text = a
                Tablo.Reveal(1, Answer1Txt.Text, Answer1CostTxt.Text, RoundScoreTxt.Text, 2)
                Answer1RevealBtn.BackColor = Color.LightGreen
            Else
                Tablo.Reveal(1, Answer1Txt.Text, Answer1CostTxt.Text, RoundScoreTxt.Text, 1)
                Answer1RevealBtn.BackColor = Color.LightGreen
            End If
        Else
            Tablo.RollBack(1, RoundScoreTxt.Text)
            Answer1RevealBtn.BackColor = Color.LightGray
        End If

    End Sub

    Private Sub Answer2RevealBtn_Click(sender As Object, e As EventArgs) Handles Answer2RevealBtn.Click
        If Answer2RevealBtn.BackColor <> Color.LightGreen Then
            If AddUpChk.Checked = True Then
                Dim a As Integer
                a = Int(RoundScoreTxt.Text) + Int(Answer2CostTxt.Text)
                RoundScoreTxt.Text = a
                Tablo.Reveal(2, Answer2Txt.Text, Answer2CostTxt.Text, RoundScoreTxt.Text, 2)
                Answer2RevealBtn.BackColor = Color.LightGreen
            Else
                Tablo.Reveal(2, Answer2Txt.Text, Answer2CostTxt.Text, RoundScoreTxt.Text, 1)
                Answer2RevealBtn.BackColor = Color.LightGreen
            End If
        Else
            Tablo.RollBack(2, RoundScoreTxt.Text)
            Answer2RevealBtn.BackColor = Color.LightGray
        End If
    End Sub

    Private Sub Answer3RevealBtn_Click(sender As Object, e As EventArgs) Handles Answer3RevealBtn.Click
        If Answer3RevealBtn.BackColor <> Color.LightGreen Then
            If AddUpChk.Checked = True Then
                Dim a As Integer
                a = Int(RoundScoreTxt.Text) + Int(Answer3CostTxt.Text)
                RoundScoreTxt.Text = a
                Tablo.Reveal(3, Answer3Txt.Text, Answer3CostTxt.Text, RoundScoreTxt.Text, 2)
                Answer3RevealBtn.BackColor = Color.LightGreen
            Else
                Tablo.Reveal(3, Answer3Txt.Text, Answer3CostTxt.Text, RoundScoreTxt.Text, 1)
                Answer3RevealBtn.BackColor = Color.LightGreen
            End If
        Else
            Tablo.RollBack(3, RoundScoreTxt.Text)
            Answer3RevealBtn.BackColor = Color.LightGray
        End If
    End Sub

    Private Sub Answer4RevealBtn_Click(sender As Object, e As EventArgs) Handles Answer4RevealBtn.Click
        If Answer4RevealBtn.BackColor <> Color.LightGreen Then
            If AddUpChk.Checked = True Then
                Dim a As Integer
                a = Int(RoundScoreTxt.Text) + Int(Answer4CostTxt.Text)
                RoundScoreTxt.Text = a
                Tablo.Reveal(4, Answer4Txt.Text, Answer4CostTxt.Text, RoundScoreTxt.Text, 2)
                Answer4RevealBtn.BackColor = Color.LightGreen
            Else
                Tablo.Reveal(4, Answer4Txt.Text, Answer4CostTxt.Text, RoundScoreTxt.Text, 1)
                Answer4RevealBtn.BackColor = Color.LightGreen
            End If
        Else
            Tablo.RollBack(4, RoundScoreTxt.Text)
            Answer4RevealBtn.BackColor = Color.LightGray
        End If
    End Sub

    Private Sub Answer5RevealBtn_Click(sender As Object, e As EventArgs) Handles Answer5RevealBtn.Click
        If Answer5RevealBtn.BackColor <> Color.LightGreen Then
            If AddUpChk.Checked = True Then
                Dim a As Integer
                a = Int(RoundScoreTxt.Text) + Int(Answer5CostTxt.Text)
                RoundScoreTxt.Text = a
                Tablo.Reveal(5, Answer5Txt.Text, Answer5CostTxt.Text, RoundScoreTxt.Text, 2)
                Answer5RevealBtn.BackColor = Color.LightGreen
            Else
                Tablo.Reveal(5, Answer5Txt.Text, Answer5CostTxt.Text, RoundScoreTxt.Text, 1)
                Answer5RevealBtn.BackColor = Color.LightGreen
            End If
        Else
            Tablo.RollBack(5, RoundScoreTxt.Text)
            Answer5RevealBtn.BackColor = Color.LightGray
        End If
    End Sub

    Private Sub Answer6RevealBtn_Click(sender As Object, e As EventArgs) Handles Answer6RevealBtn.Click
        If Answer6RevealBtn.BackColor <> Color.LightGreen Then
            If AddUpChk.Checked = True Then
                Dim a As Integer
                a = Int(RoundScoreTxt.Text) + Int(Answer6CostTxt.Text)
                RoundScoreTxt.Text = a
                Tablo.Reveal(6, Answer6Txt.Text, Answer6CostTxt.Text, RoundScoreTxt.Text, 2)
                Answer6RevealBtn.BackColor = Color.LightGreen
            Else
                Tablo.Reveal(6, Answer6Txt.Text, Answer6CostTxt.Text, RoundScoreTxt.Text, 1)
                Answer6RevealBtn.BackColor = Color.LightGreen
            End If
        Else
            Tablo.RollBack(6, RoundScoreTxt.Text)
            Answer6RevealBtn.BackColor = Color.LightGray
        End If
    End Sub

    Private Sub TabloPrepareBtn_Click(sender As Object, e As EventArgs) Handles TabloPrepareBtn.Click
        Tablo.Prepare()
    End Sub

    Private Sub GrayChk_CheckedChanged(sender As Object, e As EventArgs) Handles GrayChk.CheckedChanged
        If GrayChk.Checked = True Then
            RoundScoreTxt.ForeColor = Color.LightGray
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
        Else
            RoundScoreTxt.ForeColor = Color.Black
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
        End If
    End Sub

    Private Sub BigRoundBtn_Click(sender As Object, e As EventArgs) Handles BigRoundBtn.Click
        BigGameControl.Show()
        BigGameTablo.Show()
    End Sub

    Private Sub FirstRoundBtn_Click(sender As Object, e As EventArgs) Handles FirstRoundBtn.Click
        Dim Answers1() As String = Answers(0).Split("&")
        Dim Answers2() As String = Answers(1).Split("&")
        Dim Answers3() As String = Answers(2).Split("&")
        Dim Answers4() As String = Answers(3).Split("&")
        Dim Answers5() As String = Answers(4).Split("&")
        Dim Answers6() As String = Answers(5).Split("&")
        Answer1Txt.Text = Answers1(0)
        Answer2Txt.Text = Answers2(0)
        Answer3Txt.Text = Answers3(0)
        Answer4Txt.Text = Answers4(0)
        Answer5Txt.Text = Answers5(0)
        Answer6Txt.Text = Answers6(0)
        Answer1CostTxt.Text = Answers1(1)
        Answer2CostTxt.Text = Answers2(1)
        Answer3CostTxt.Text = Answers3(1)
        Answer4CostTxt.Text = Answers4(1)
        Answer5CostTxt.Text = Answers5(1)
        Answer6CostTxt.Text = Answers6(1)
        Tablo.Prepare()
    End Sub

    Private Sub SecondRoundBtn_Click(sender As Object, e As EventArgs) Handles SecondRoundBtn.Click
        Dim Answers1() As String = Answers(6).Split("&")
        Dim Answers2() As String = Answers(7).Split("&")
        Dim Answers3() As String = Answers(8).Split("&")
        Dim Answers4() As String = Answers(9).Split("&")
        Dim Answers5() As String = Answers(10).Split("&")
        Dim Answers6() As String = Answers(11).Split("&")
        Answer1Txt.Text = Answers1(0)
        Answer2Txt.Text = Answers2(0)
        Answer3Txt.Text = Answers3(0)
        Answer4Txt.Text = Answers4(0)
        Answer5Txt.Text = Answers5(0)
        Answer6Txt.Text = Answers6(0)
        Answer1CostTxt.Text = Answers1(1)
        Answer2CostTxt.Text = Answers2(1)
        Answer3CostTxt.Text = Answers3(1)
        Answer4CostTxt.Text = Answers4(1)
        Answer5CostTxt.Text = Answers5(1)
        Answer6CostTxt.Text = Answers6(1)
        Tablo.Prepare()
    End Sub

    Private Sub ThirdRoundBtn_Click(sender As Object, e As EventArgs) Handles ThirdRoundBtn.Click
        Dim Answers1() As String = Answers(12).Split("&")
        Dim Answers2() As String = Answers(13).Split("&")
        Dim Answers3() As String = Answers(14).Split("&")
        Dim Answers4() As String = Answers(15).Split("&")
        Dim Answers5() As String = Answers(16).Split("&")
        Dim Answers6() As String = Answers(17).Split("&")
        Answer1Txt.Text = Answers1(0)
        Answer2Txt.Text = Answers2(0)
        Answer3Txt.Text = Answers3(0)
        Answer4Txt.Text = Answers4(0)
        Answer5Txt.Text = Answers5(0)
        Answer6Txt.Text = Answers6(0)
        Answer1CostTxt.Text = Answers1(1)
        Answer2CostTxt.Text = Answers2(1)
        Answer3CostTxt.Text = Answers3(1)
        Answer4CostTxt.Text = Answers4(1)
        Answer5CostTxt.Text = Answers5(1)
        Answer6CostTxt.Text = Answers6(1)
        Tablo.Prepare()
    End Sub

    Private Sub FourthRoundBtn_Click(sender As Object, e As EventArgs) Handles FourthRoundBtn.Click
        Dim Answers1 As String = Answers(18)
        Dim Answers2 As String = Answers(19)
        Dim Answers3 As String = Answers(20)
        Dim Answers4 As String = Answers(21)
        Dim Answers5 As String = Answers(22)
        Dim Answers6 As String = Answers(23)
        Answer1Txt.Text = Answers1
        Answer2Txt.Text = Answers2
        Answer3Txt.Text = Answers3
        Answer4Txt.Text = Answers4
        Answer5Txt.Text = Answers5
        Answer6Txt.Text = Answers6
        Answer1CostTxt.Text = 15
        Answer2CostTxt.Text = 30
        Answer3CostTxt.Text = 45
        Answer4CostTxt.Text = 60
        Answer5CostTxt.Text = 120
        Answer6CostTxt.Text = 240
        Tablo.Prepare()
    End Sub
End Class
