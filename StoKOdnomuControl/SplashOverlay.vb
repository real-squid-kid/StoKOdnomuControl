Option Strict On
Imports System.Drawing
Imports System.Drawing.Text
Imports System.Windows.Forms

' Отдельный прозрачный слой: ответы под ним скрываются, но не переписываются.
Public NotInheritable Class SplashOverlay
    Inherits Control
    Private ReadOnly _designFont As New Font(FontFamily.GenericMonospace, 56, FontStyle.Bold)

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.SupportsTransparentBackColor Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.ResizeRedraw, True)
        BackColor = Color.Transparent
        Font = _designFont
        Name = "SplashOverlay"
        AccessibleRole = AccessibleRole.StaticText
        AccessibleName = "Заставка"
        TabStop = False
        Visible = False
        AddHandler TextChanged, Sub() Invalidate()
        AddHandler Disposed, Sub() _designFont.Dispose()
    End Sub

    Public Shared Function AddTo(form As Form, answers As IEnumerable(Of Control)) As SplashOverlay
        Dim area = answers.Select(Function(control) control.Bounds).Aggregate(Function(left, right) Rectangle.Union(left, right))
        Dim overlay As New SplashOverlay With {.Bounds = area, .ForeColor = answers.First().ForeColor}
        form.Controls.Add(overlay)
        Return overlay
    End Function

    Public Sub Render(state As SplashState)
        Text = state.Line1 & vbLf & state.Line2
        Visible = state.Visible
        If Visible Then BringToFront()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        Dim lines = Text.Split(ChrW(10))
        If lines.Length <> 2 OrElse ClientSize.Width < 2 OrElse ClientSize.Height < 2 Then Return
        e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit
        Using format As New StringFormat With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center, .FormatFlags = StringFormatFlags.NoWrap}
            Dim width = lines.Max(Function(line) e.Graphics.MeasureString(line, Font, Integer.MaxValue, format).Width)
            Dim ratio = Math.Min(1.0F, Math.Min(Math.Max(1, ClientSize.Width - 20) / Math.Max(1.0F, width), Math.Max(1, ClientSize.Height - 20) / (Font.GetHeight(e.Graphics) * 2.15F)))
            Using fitted As New Font(Font.FontFamily, Math.Max(1.0F, Font.Size * ratio), Font.Style), brush As New SolidBrush(ForeColor)
                Dim height = fitted.GetHeight(e.Graphics)
                Dim gap = height * 0.15F
                Dim top = (ClientSize.Height - height * 2 - gap) / 2
                For i = 0 To 1
                    e.Graphics.DrawString(lines(i), fitted, brush, New RectangleF(0, top + i * (height + gap), ClientSize.Width, height), format)
                Next
            End Using
        End Using
    End Sub
End Class
