Option Strict On
Imports System.Drawing
Imports System.Drawing.Text
Imports System.IO
Imports System.Windows.Forms

' One proportional canvas keeps the labels aligned with the artwork at any display size.
Public NotInheritable Class BoardLayout
    Implements IDisposable
    Private ReadOnly _form As Form
    Private ReadOnly _canvas As New Panel()
    Private ReadOnly _bounds As New Dictionary(Of Control, Rectangle)()
    Private ReadOnly _fontSizes As New Dictionary(Of Control, Single)()
    Private ReadOnly _fontStyles As New Dictionary(Of Control, FontStyle)()
    Private ReadOnly _fonts As New PrivateFontCollection()
    Private ReadOnly _ownedFonts As New Dictionary(Of Control, Font)()
    Private _fullScreen As Boolean

    Public Sub New(form As Form)
        _form = form
        form.AutoScaleMode = AutoScaleMode.None
        form.KeyPreview = True
        form.BackColor = Color.Black
        form.MinimumSize = New Size(640, 380)
        _canvas.BackgroundImage = form.BackgroundImage
        _canvas.BackgroundImageLayout = ImageLayout.Stretch
        form.BackgroundImage = Nothing
        For Each control As Control In form.Controls.Cast(Of Control)().ToArray()
            _bounds.Add(control, control.Bounds)
            _fontSizes.Add(control, control.Font.SizeInPoints)
            _fontStyles.Add(control, control.Font.Style)
            Dim label = TryCast(control, Label)
            If label IsNot Nothing Then
                label.UseCompatibleTextRendering = True
                label.AutoEllipsis = True
            End If
            _canvas.Controls.Add(control)
        Next
        form.Controls.Add(_canvas)
        For Each name In {"JetBrainsMono-Regular.ttf", "JetBrainsMono-Bold.ttf"}
            Dim path = IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts", name)
            If File.Exists(path) Then _fonts.AddFontFile(path)
        Next
        AddHandler form.ClientSizeChanged, AddressOf ResizeCanvas
        AddHandler form.KeyDown, AddressOf OnKeyDown
        AddHandler form.FormClosing, AddressOf OnClosing
        ResizeCanvas(Nothing, EventArgs.Empty)
    End Sub

    Public Sub ShowOn(screen As Screen, fullscreen As Boolean)
        _fullScreen = fullscreen
        _form.WindowState = FormWindowState.Normal
        _form.FormBorderStyle = If(fullscreen, FormBorderStyle.None, FormBorderStyle.Sizable)
        _form.StartPosition = FormStartPosition.Manual
        _form.Bounds = If(fullscreen, screen.Bounds, New Rectangle(screen.WorkingArea.Location, New Size(Math.Min(1280, screen.WorkingArea.Width), Math.Min(720, screen.WorkingArea.Height))))
        _form.Show()
        _form.BringToFront()
    End Sub

    Public Sub RefreshText()
        ResizeCanvas(Nothing, EventArgs.Empty)
    End Sub

    Private Sub ResizeCanvas(sender As Object, e As EventArgs)
        Dim scale = Math.Min(_form.ClientSize.Width / 1264.0F, _form.ClientSize.Height / 681.0F)
        If scale <= 0 Then Return
        _canvas.Bounds = New Rectangle(CInt((_form.ClientSize.Width - 1264 * scale) / 2), CInt((_form.ClientSize.Height - 681 * scale) / 2), CInt(1264 * scale), CInt(681 * scale))
        Using graphics = _canvas.CreateGraphics()
            For Each pair In _bounds
                Dim control = pair.Key
                Dim design = pair.Value
                control.Bounds = New Rectangle(CInt(design.X * scale), CInt(design.Y * scale), CInt(design.Width * scale), CInt(design.Height * scale))
                Dim family = If(_fonts.Families.Length > 0, _fonts.Families(0), FontFamily.GenericMonospace)
                Dim size = Math.Max(8.0F, _fontSizes(control) * scale)
                Dim font As New Font(family, size, _fontStyles(control))
                While control.Text.Length > 0 AndAlso size > 8 AndAlso graphics.MeasureString(control.Text, font).Width > control.Width - 6
                    font.Dispose()
                    size = Math.Max(8.0F, size - 1)
                    font = New Font(family, size, _fontStyles(control))
                End While
                If control.Font.Equals(font) Then
                    font.Dispose()
                Else
                    Dim oldFont As Font = Nothing
                    _ownedFonts.TryGetValue(control, oldFont)
                    control.Font = font
                    _ownedFonts(control) = font
                    If oldFont IsNot Nothing Then oldFont.Dispose()
                End If
            Next
        End Using
    End Sub

    Private Sub OnKeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.Escape AndAlso _fullScreen Then
            ShowOn(Screen.FromControl(_form), False)
            e.Handled = True
        End If
    End Sub

    Private Sub OnClosing(sender As Object, e As FormClosingEventArgs)
        If e.CloseReason = CloseReason.UserClosing Then
            e.Cancel = True
            _form.Hide()
        End If
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        RemoveHandler _form.ClientSizeChanged, AddressOf ResizeCanvas
        RemoveHandler _form.KeyDown, AddressOf OnKeyDown
        RemoveHandler _form.FormClosing, AddressOf OnClosing
        For Each font In _ownedFonts.Values
            font.Dispose()
        Next
        _ownedFonts.Clear()
        _fonts.Dispose()
    End Sub
End Class
