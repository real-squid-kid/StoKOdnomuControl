Public Class RunForm
    Private Sub PictureBox1_Click(sender As Object, e As EventArgs) Handles PictureBox1.Click
        AudioService.Instance.StopChannel(AudioChannel.EasterEgg)
        Me.Close()
    End Sub

    Private Sub RunForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        AudioService.Instance.Play(SoundCue.EasterEgg, AudioChannel.EasterEgg)
    End Sub

    Private Sub RunForm_FormClosed(sender As Object, e As FormClosedEventArgs) Handles MyBase.FormClosed
        AudioService.Instance.StopChannel(AudioChannel.EasterEgg)
    End Sub
End Class
