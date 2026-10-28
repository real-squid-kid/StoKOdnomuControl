Option Strict On
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms
Imports StoKOdnomuControl

Friend Module DisplayScaleChecks
    Public Sub Run(directory As String, check As Action(Of Boolean, String), withUi As Boolean)
        check(DisplayScaleWarning.BuildMessage(New Dictionary(Of String, Integer) From {{"Экран 1", 100}, {"Экран 2", 100}}) = "", "no scale warning when all displays are 100 percent")
        For Each scale In {125, 150, 200}
            Dim text = DisplayScaleWarning.BuildMessage(New Dictionary(Of String, Integer) From {{"Экран 1", 100}, {"Экран 2", scale}})
            check(text.Contains("Экран 2: " & scale & "%") AndAlso Not text.Contains("Экран 1: 100%"), "warn for non-primary display at " & scale)
            check(text.Contains("Пуск → Параметры → Система") AndAlso text.Contains("Масштаб") AndAlso text.Contains("Разрешение экрана менять не требуется"), "warning gives scale settings instructions")
        Next
        check(DisplayScaleWarning.BuildMessage(New Dictionary(Of String, Integer) From {{"Экран 1", 75}}) <> "", "scale below 100 also warns")
        Dim detected = DisplayScaleWarning.ReadScales()
        check(detected.Count = Screen.AllScreens.Length AndAlso detected.Values.All(Function(scale) scale > 0), "native scale query includes connected displays")
        Console.WriteLine("Display scale query: " & String.Join("; ", detected.Select(Function(pair) pair.Key & "=" & pair.Value & "%")))
        If Not withUi Then Return
        IO.Directory.CreateDirectory(directory)
        Dim message = DisplayScaleWarning.BuildMessage(New Dictionary(Of String, Integer) From {{"Экран 1 (основной)", 125}, {"Экран 2", 150}})
        Using owner As New Form With {.Opacity = 0, .ShowInTaskbar = False}, dialog = DisplayScaleWarning.CreateDialog(message)
            owner.Show()
            AddHandler dialog.Shown, Sub()
                                         Try
                                             check(Screen.FromControl(dialog).Primary, "scale warning opens on primary monitor")
                                             check(DirectCast(dialog.AcceptButton, Button).Text = "Продолжить", "scale warning permits continuation")
                                             Using bitmap As New Bitmap(dialog.Width, dialog.Height)
                                                 dialog.DrawToBitmap(bitmap, New Rectangle(Point.Empty, bitmap.Size))
                                                 bitmap.Save(Path.Combine(directory, "display-scale-warning.png"))
                                             End Using
                                         Finally
                                             dialog.DialogResult = DialogResult.OK
                                         End Try
                                     End Sub
            check(OperatorWindows.ShowModal(dialog, owner) = DialogResult.OK, "scale warning continues without changing Windows settings")
        End Using
        Console.WriteLine("Display scale warning preview: " & directory)
    End Sub
End Module
