Option Strict On
Imports System.Drawing
Imports System.Drawing.Text
Imports System.Drawing.Drawing2D
Imports System.IO
Imports System.Windows.Forms

' Общий холст масштабирует фон и подписи в одной системе координат 1264x681.
' Свободное место при другом соотношении сторон остаётся чёрным, без искажения макета.
Public NotInheritable Class BoardLayout
    Implements IDisposable
    Private ReadOnly _form As Form
    Private ReadOnly _canvas As New Panel()
    Private ReadOnly _bounds As New Dictionary(Of Control, Rectangle)()
    Private ReadOnly _fontSizes As New Dictionary(Of Control, Single)()
    Private ReadOnly _fontStyles As New Dictionary(Of Control, FontStyle)()
    Private ReadOnly _fonts As New PrivateFontCollection()
    ' Освобождаем только созданные нами Font, а не унаследованный/общий Font формы.
    Private ReadOnly _ownedFonts As New Dictionary(Of Control, Font)()
    Private Structure TextFit
        Public Text As String
        Public Size As Size
        Public Scale As Single
        Public Dpi As Single
    End Structure
    Private ReadOnly _fits As New Dictionary(Of Control, TextFit)()
    Private _backgroundSource As Image
    Private _backgroundCache As Bitmap
    Private _backgroundDirty As Boolean = True
    Private _fullScreen As Boolean
    Private ReadOnly _hideOnClose As Boolean

    Public Sub New(form As Form, Optional hideOnClose As Boolean = True)
        _form = form
        _hideOnClose = hideOnClose
        form.AutoScaleMode = AutoScaleMode.None
        form.KeyPreview = True
        form.BackColor = Color.Black
        form.MinimumSize = New Size(640, 380)
        _backgroundSource = form.BackgroundImage
        _canvas.BackColor = Color.Black
        _canvas.BackgroundImageLayout = ImageLayout.None
        form.BackgroundImage = Nothing
        For Each control As Control In form.Controls.Cast(Of Control)().ToArray()
            ' Геометрия Designer запоминается один раз. Повторный resize всегда считает
            ' координаты от оригинала, а не накапливает погрешность предыдущих масштабирований.
            _bounds.Add(control, control.Bounds)
            _fontSizes.Add(control, control.Font.SizeInPoints)
            _fontStyles.Add(control, control.Font.Style)
            Dim label = TryCast(control, Label)
            If label IsNot Nothing Then
                ' Частные шрифты рисуются GDI+: одной загрузки TTF недостаточно для обычного GDI.
                label.UseCompatibleTextRendering = True
                label.AutoEllipsis = True
            End If
            _canvas.Controls.Add(control)
        Next
        form.Controls.Add(_canvas)
        For Each name In {"JetBrainsMono-Regular.ttf", "JetBrainsMono-Bold.ttf"}
            ' Эти файлы входят в комплект; установки всего семейства в Windows не требуется.
            Dim path = IO.Path.Combine(ApplicationFiles.FontsDirectory, name)
            If File.Exists(path) Then _fonts.AddFontFile(path)
        Next
        AddHandler form.ClientSizeChanged, AddressOf ResizeCanvas
        AddHandler form.KeyDown, AddressOf OnKeyDown
        AddHandler form.FormClosing, AddressOf OnClosing
        ResizeCanvas(Nothing, EventArgs.Empty)
    End Sub

    Public Sub ShowOn(screen As Screen, fullscreen As Boolean)
        ' Полный экран занимает Bounds монитора, оконный режим — его WorkingArea.
        _fullScreen = fullscreen
        _form.WindowState = FormWindowState.Normal
        _form.FormBorderStyle = If(fullscreen, FormBorderStyle.None, FormBorderStyle.Sizable)
        _form.StartPosition = FormStartPosition.Manual
        _form.Bounds = If(fullscreen, screen.Bounds, New Rectangle(screen.WorkingArea.Location, New Size(Math.Min(1280, screen.WorkingArea.Width), Math.Min(720, screen.WorkingArea.Height))))
        _form.Show()
        _form.BringToFront()
    End Sub

    Public Sub ApplyAppearance(background As Image, textColor As Color)
        If Not Object.ReferenceEquals(_backgroundSource, background) Then
            _backgroundSource = background
            _backgroundDirty = True
        End If
        RefreshBackground()
        For Each control In _bounds.Keys
            If Not TypeOf control Is PictureBox Then control.ForeColor = textColor
        Next
        _canvas.Invalidate(True)
    End Sub

    Public Sub RefreshText()
        ResizeCanvas(Nothing, EventArgs.Empty)
    End Sub

    ' Подвижные подписи задают положение в исходном холсте, а не в текущих
    ' пикселях окна. Следующий рендер/resize масштабирует его без накопления сдвига.
    Public Sub SetDesignBounds(control As Control, bounds As Rectangle)
        If Not _bounds.ContainsKey(control) Then Throw New ArgumentException("Элемент не принадлежит холсту.", NameOf(control))
        _bounds(control) = bounds
    End Sub

    ' Прозрачные Label перерисовывают фон родителя. Сохраняем уже вписанный
    ' фон в пикселях холста: каждой подписи не нужно снова уменьшать большой JPEG.
    ' Исходная Image заимствована у профиля, а этим Bitmap владеет только layout.
    Private Sub RefreshBackground()
        If _canvas.Width < 1 OrElse _canvas.Height < 1 Then Return
        If Not _backgroundDirty AndAlso _backgroundCache IsNot Nothing AndAlso _backgroundCache.Size = _canvas.Size Then Return
        Dim replacement As New Bitmap(_canvas.Width, _canvas.Height)
        Try
            Using graphics = System.Drawing.Graphics.FromImage(replacement)
                graphics.Clear(Color.Black)
                If _backgroundSource IsNot Nothing Then
                    Dim ratio = Math.Min(_canvas.Width / CDbl(_backgroundSource.Width), _canvas.Height / CDbl(_backgroundSource.Height))
                    Dim width = CInt(_backgroundSource.Width * ratio)
                    Dim height = CInt(_backgroundSource.Height * ratio)
                    Dim area As New Rectangle((_canvas.Width - width) \ 2, (_canvas.Height - height) \ 2, width, height)
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic
                    graphics.DrawImage(_backgroundSource, area)
                End If
            End Using
            Dim previous = _backgroundCache
            _canvas.BackgroundImage = replacement
            _backgroundCache = replacement
            _backgroundDirty = False
            If previous IsNot Nothing Then previous.Dispose()
        Catch
            replacement.Dispose()
            Throw
        End Try
    End Sub

    Private Sub ResizeCanvas(sender As Object, e As EventArgs)
        Dim scale = Math.Min(_form.ClientSize.Width / 1264.0F, _form.ClientSize.Height / 681.0F)
        If scale <= 0 Then Return
        _canvas.Bounds = New Rectangle(CInt((_form.ClientSize.Width - 1264 * scale) / 2), CInt((_form.ClientSize.Height - 681 * scale) / 2), CInt(1264 * scale), CInt(681 * scale))
        RefreshBackground()
        Using graphics = _canvas.CreateGraphics()
            For Each pair In _bounds
                Dim control = pair.Key
                Dim design = pair.Value
                control.Bounds = New Rectangle(CInt(design.X * scale), CInt(design.Y * scale), CInt(design.Width * scale), CInt(design.Height * scale))
                ' PictureBox.Zoom масштабирует всю картинку вместе с её областью,
                ' сохраняя пропорции. Font для маркера не используется.
                If TypeOf control Is PictureBox Then Continue For
                Dim previousFit As New TextFit()
                If _fits.TryGetValue(control, previousFit) AndAlso previousFit.Text = control.Text AndAlso previousFit.Size = control.Size AndAlso previousFit.Scale = scale AndAlso previousFit.Dpi = graphics.DpiX Then Continue For
                Dim family = If(_fonts.Families.Length > 0, _fonts.Families(0), FontFamily.GenericMonospace)
                Dim size = Math.Max(8.0F, _fontSizes(control) * scale)
                Dim font As New Font(family, size, _fontStyles(control))
                ' Длинный текст уменьшается до нижнего порога; AutoEllipsis остаётся страховкой.
                While control.Text.Length > 0 AndAlso size > 8 AndAlso graphics.MeasureString(control.Text, font).Width > control.Width - 6
                    font.Dispose()
                    size = Math.Max(8.0F, size - 1)
                    font = New Font(family, size, _fontStyles(control))
                End While
                If control.Font.Equals(font) Then
                    ' WinForms может проигнорировать присваивание равного Font. В этой ветке
                    ' уничтожаем НОВЫЙ лишний объект, иначе уничтожили бы ещё используемый Font.
                    font.Dispose()
                Else
                    Dim oldFont As Font = Nothing
                    _ownedFonts.TryGetValue(control, oldFont)
                    control.Font = font
                    _ownedFonts(control) = font
                    If oldFont IsNot Nothing Then oldFont.Dispose()
                End If
                _fits(control) = New TextFit With {.Text = control.Text, .Size = control.Size, .Scale = scale, .Dpi = graphics.DpiX}
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
        ' Крестик скрывает зрительское окно, а не теряет его состояние/экземпляр.
        ' При завершении приложения владелец вызывает Dispose явно.
        If _hideOnClose AndAlso e.CloseReason = CloseReason.UserClosing Then
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
        _fits.Clear()
        _canvas.BackgroundImage = Nothing
        If _backgroundCache IsNot Nothing Then _backgroundCache.Dispose()
        _backgroundCache = Nothing
        _backgroundSource = Nothing
        _fonts.Dispose()
    End Sub
End Class
