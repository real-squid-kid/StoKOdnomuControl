Public Class BigGameControl
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
End Class