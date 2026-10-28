Option Strict On

Public NotInheritable Class AboutBox1
    Public Sub New()
        InitializeComponent()
        OperatorWindows.Install(Me)
        Text = "О программе"
        LabelProductName.Text = "Сто к одному"
        LabelVersion.Text = "Версия " & GetType(AboutBox1).Assembly.GetName().Version.ToString(3) & " · Windows · .NET Framework 4.8"
        LabelCopyright.Text = "Исходный проект: real-squid-kid" & Environment.NewLine & "Оригинальная идея: StereoMaster / Gameshows.ru"
        LabelCompanyName.Text = "Лицензия: MIT · Vasily Makrov"
        TextBoxDescription.Text = "Пульт оператора и зрительские табло основной и большой игры." & Environment.NewLine & "Ответы, очки и промахи, таймер 20/25 секунд и кнопки Buzz!." & Environment.NewLine & "Профили оформления, редактор баз, заставки и отмена игровых действий."
    End Sub

    Private Sub OKButton_Click(sender As Object, e As EventArgs) Handles OKButton.Click
        Close()
    End Sub

    Private Sub RunLbl_Click(sender As Object, e As EventArgs) Handles RunLbl.Click
        RunForm.Show()
    End Sub
End Class
