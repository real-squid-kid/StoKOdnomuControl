Option Strict On
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Security.Cryptography
Imports System.Xml
Imports System.Xml.Serialization

Public Class BoardStyle
    Public Property Background As String = ""
    Public Property LeftClear As String = ""
    Public Property LeftStrike As String = ""
    Public Property RightClear As String = ""
    Public Property RightStrike As String = ""
    <XmlIgnore>
    Public Property TextArgb As Integer = Color.FromArgb(255, 240, 190).ToArgb()
    Public Property TextColor As String
        Get
            Return "#" & (TextArgb And &HFFFFFF).ToString("X6", Globalization.CultureInfo.InvariantCulture)
        End Get
        Set(value As String)
            Dim rgb As Integer
            If value Is Nothing OrElse value.Length <> 7 OrElse value(0) <> "#"c OrElse Not Integer.TryParse(value.Substring(1), Globalization.NumberStyles.HexNumber, Globalization.CultureInfo.InvariantCulture, rgb) Then Throw New InvalidDataException("Цвет текста задаётся как #RRGGBB.")
            TextArgb = Color.FromArgb(255, (rgb >> 16) And 255, (rgb >> 8) And 255, rgb And 255).ToArgb()
        End Set
    End Property

    ' Старые XML с целочисленным TextArgb продолжают читаться; новые пишут #RRGGBB.
    <XmlElement("TextArgb")>
    Public Property LegacyTextArgb As Integer
        Get
            Return TextArgb
        End Get
        Set(value As Integer)
            TextArgb = value
        End Set
    End Property
    Public Function ShouldSerializeLegacyTextArgb() As Boolean
        Return False
    End Function
    Public Property Inherit As Boolean = True

    Public Function Assets() As String()
        Return {Background, LeftClear, LeftStrike, RightClear, RightStrike}
    End Function

    Public Sub SetAsset(index As Integer, value As String)
        Select Case index
            Case 0 : Background = value
            Case 1 : LeftClear = value
            Case 2 : LeftStrike = value
            Case 3 : RightClear = value
            Case 4 : RightStrike = value
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(index))
        End Select
    End Sub
End Class

Public Class AppearanceProfile
    Public Property Version As Integer = 1
    Public Property Name As String = "По умолчанию"
    Public Property Main As New BoardStyle()
    Public Property Rounds As BoardStyle() = {New BoardStyle(), New BoardStyle(), New BoardStyle(), New BoardStyle()}
    Public Property Big As New BoardStyle()
    Public Property ScreenIndex As Integer = 0
    Public Property FullScreen As Boolean = True
    Public Property SoundEnabled As Boolean = True
    ' Отсутствие поля в прежних config.xml оставляет стандартные 250 мс.
    Public Property BuzzLampDelayMilliseconds As Integer = BuzzManager.DefaultLampDelayMilliseconds

    Public Function Styles() As BoardStyle()
        Return New BoardStyle() {Main}.Concat(Rounds).Concat({Big}).ToArray()
    End Function

    Public Function MainStyle(round As Integer) As BoardStyle
        Return If(round >= 1 AndAlso round <= 4 AndAlso Not Rounds(round - 1).Inherit, Rounds(round - 1), Main)
    End Function
End Class

' Только этот объект владеет импортированными Bitmap. Комплектные My.Resources
' остаются общими; табло заимствуют картинки и не уничтожают их при смене раунда.
Public NotInheritable Class LoadedAppearance
    Implements IDisposable
    Private ReadOnly _images As New Dictionary(Of String, Bitmap)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _encoded As New Dictionary(Of String, Byte())(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _extensions As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _profile As AppearanceProfile
    Public ReadOnly Property Profile As AppearanceProfile
        Get
            Return AppearanceStorage.Clone(_profile)
        End Get
    End Property

    Public Sub New(profile As AppearanceProfile, Optional keepEncoded As Boolean = False)
        AppearanceStorage.Validate(profile)
        _profile = AppearanceStorage.Clone(profile)
        Try
            For Each style In _profile.Styles()
                For Each path In style.Assets()
                    If path = "" OrElse _images.ContainsKey(path) Then Continue For
                    Dim info As New FileInfo(path)
                    If Not info.Exists OrElse info.Length > 64L * 1024 * 1024 Then Throw New InvalidDataException("Изображение отсутствует или больше 64 МБ: " & path)
                    Dim extension = IO.Path.GetExtension(path).ToLowerInvariant()
                    If Not {".png", ".jpg", ".jpeg"}.Contains(extension) Then Throw New InvalidDataException("Выберите PNG или JPEG: " & path)
                    Try
                        Dim bytes = File.ReadAllBytes(path)
                        Using stream As New MemoryStream(bytes, False), source = Image.FromStream(stream, True, True)
                            If Not source.RawFormat.Equals(ImageFormat.Png) AndAlso Not source.RawFormat.Equals(ImageFormat.Jpeg) Then Throw New InvalidDataException("Файл не является PNG/JPEG: " & path)
                            If CLng(source.Width) * source.Height > 16000000 Then Throw New InvalidDataException("Картинка превышает 16 миллионов пикселей: " & path)
                            _images.Add(path, New Bitmap(source))
                            If keepEncoded Then
                                _encoded.Add(path, bytes)
                                _extensions.Add(path, If(source.RawFormat.Equals(ImageFormat.Png), ".png", ".jpg"))
                            End If
                        End Using
                    Catch ex As Exception When TypeOf ex Is ArgumentException OrElse TypeOf ex Is OutOfMemoryException
                        Throw New InvalidDataException("Не удалось декодировать PNG/JPEG: " & path, ex)
                    End Try
                Next
            Next
        Catch
            Dispose()
            Throw
        End Try
    End Sub

    Public Function Picture(path As String, fallback As Image) As Image
        Return If(path = "", fallback, _images(path))
    End Function

    Friend Function EncodedPicture(path As String) As Byte()
        Return _encoded(path)
    End Function

    Friend Function PictureExtension(path As String) As String
        Return _extensions(path)
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        _encoded.Clear()
        _extensions.Clear()
        For Each bitmap In _images.Values
            bitmap.Dispose()
        Next
        _images.Clear()
    End Sub
End Class

Public Module AppearanceStorage
    Private ReadOnly Serializer As New XmlSerializer(GetType(AppearanceProfile))

    Public Function ConfigPath(folder As String) As String
        Dim preferred = IO.Path.Combine(folder, "config.xml")
        If File.Exists(preferred) Then Return preferred
        Dim legacy = IO.Path.Combine(folder, "profile.xml")
        Return If(File.Exists(legacy), legacy, preferred)
    End Function

    Public Function Equivalent(left As AppearanceProfile, right As AppearanceProfile) As Boolean
        Using a As New MemoryStream(), b As New MemoryStream()
            Serializer.Serialize(a, left)
            Serializer.Serialize(b, right)
            Return a.ToArray().SequenceEqual(b.ToArray())
        End Using
    End Function

    Public Function Clone(profile As AppearanceProfile) As AppearanceProfile
        Return New AppearanceProfile With {
            .Version = profile.Version, .Name = profile.Name,
            .Main = CopyStyle(profile.Main), .Big = CopyStyle(profile.Big),
            .Rounds = profile.Rounds.Select(Function(style) CopyStyle(style)).ToArray(),
            .ScreenIndex = profile.ScreenIndex, .FullScreen = profile.FullScreen,
            .SoundEnabled = profile.SoundEnabled, .BuzzLampDelayMilliseconds = profile.BuzzLampDelayMilliseconds}
    End Function

    Private Function CopyStyle(style As BoardStyle) As BoardStyle
        Return New BoardStyle With {.Background = style.Background,
            .LeftClear = style.LeftClear, .LeftStrike = style.LeftStrike,
            .RightClear = style.RightClear, .RightStrike = style.RightStrike,
            .TextArgb = style.TextArgb, .Inherit = style.Inherit}
    End Function

    Public Sub Validate(profile As AppearanceProfile)
        If profile Is Nothing OrElse profile.Version <> 1 Then Throw New InvalidDataException("Неподдерживаемая версия профиля оформления.")
        If String.IsNullOrWhiteSpace(profile.Name) OrElse profile.Name.Length > 80 Then Throw New InvalidDataException("Введите имя профиля от 1 до 80 символов.")
        If profile.Main Is Nothing OrElse profile.Big Is Nothing OrElse profile.Rounds Is Nothing OrElse profile.Rounds.Length <> 4 OrElse profile.Rounds.Any(Function(s) s Is Nothing) Then Throw New InvalidDataException("Неполный профиль оформления.")
        If profile.BuzzLampDelayMilliseconds < 0 OrElse profile.BuzzLampDelayMilliseconds > BuzzManager.MaxLampDelayMilliseconds Then Throw New InvalidDataException("Задержка лампы Buzz! должна быть от 0 до 5000 мс.")
        If profile.ScreenIndex < 0 Then Throw New InvalidDataException("Неверный номер экрана.")
        For Each style In profile.Styles()
            If style.Assets().Any(Function(p) p Is Nothing) Then Throw New InvalidDataException("Не задан путь изображения.")
            If Color.FromArgb(style.TextArgb).A <> 255 Then Throw New InvalidDataException("Цвет текста должен быть непрозрачным.")
        Next
    End Sub

    Public Function Open(path As String) As AppearanceProfile
        Dim profile As AppearanceProfile
        Using reader = XmlReader.Create(path, New XmlReaderSettings With {.DtdProcessing = DtdProcessing.Prohibit, .XmlResolver = Nothing, .MaxCharactersInDocument = 128000})
            profile = DirectCast(Serializer.Deserialize(reader), AppearanceProfile)
        End Using
        If profile.Name = "Встроенное оформление" Then profile.Name = "По умолчанию"
        Validate(profile)
        Dim folder = IO.Path.GetDirectoryName(IO.Path.GetFullPath(path))
        For Each style In profile.Styles()
            Dim assets = style.Assets()
            For i = 0 To assets.Length - 1
                If assets(i) <> "" Then style.SetAsset(i, ResolveMedia(folder, assets(i)))
            Next
        Next
        ' Проверка картинок до возврата не даёт открыть наполовину рабочий профиль.
        Using loaded As New LoadedAppearance(profile)
        End Using
        Return profile
    End Function

    Private Function ResolveMedia(folder As String, relative As String) As String
        If IO.Path.IsPathRooted(relative) Then Throw New InvalidDataException("В переносимом профиле допустимы только относительные пути.")
        Dim root = IO.Path.GetFullPath(folder).TrimEnd(IO.Path.DirectorySeparatorChar) & IO.Path.DirectorySeparatorChar
        Dim full = IO.Path.GetFullPath(IO.Path.Combine(root, relative))
        If Not full.StartsWith(root, StringComparison.OrdinalIgnoreCase) Then Throw New InvalidDataException("Путь медиа выходит за папку профиля.")
        ' Ссылки и junction не должны обходить ограничение относительного пути.
        Dim current = full
        While current.Length >= root.Length
            If File.Exists(current) OrElse Directory.Exists(current) Then
                If (File.GetAttributes(current) And FileAttributes.ReparsePoint) <> 0 Then Throw New InvalidDataException("Ссылки в папке медиа не поддерживаются.")
            End If
            current = IO.Path.GetDirectoryName(current)
        End While
        Return full
    End Function

    Public Function Save(profile As AppearanceProfile, folder As String) As LoadedAppearance
        Validate(profile)
        Dim candidate = Clone(profile)
        Using prepared As New LoadedAppearance(candidate, keepEncoded:=True)
            Dim media = IO.Path.Combine(folder, "media")
            Directory.CreateDirectory(media)
            Dim paths As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            Dim created As New List(Of String)()
            Dim ready As LoadedAppearance = Nothing
            Dim committed As Boolean
            Try
                For Each style In candidate.Styles()
                    Dim assets = style.Assets()
                    For i = 0 To assets.Length - 1
                        Dim source = assets(i)
                        If source = "" Then Continue For
                        If Not paths.ContainsKey(source) Then
                            ' Адрес по содержимому исключает новые копии при переключении
                            ' профилей. Исходный JPEG/PNG сохраняется побайтно, без перекодирования.
                            Dim bytes = prepared.EncodedPicture(source)
                            Dim digest As String
                            Using hash = SHA256.Create()
                                digest = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant()
                            End Using
                            Dim relative = IO.Path.Combine("media", digest & prepared.PictureExtension(source))
                            Dim target = IO.Path.Combine(folder, relative)
                            If Not File.Exists(target) Then
                                created.Add(target)
                                AtomicFile.Write(target, bytes)
                            ElseIf Not bytes.SequenceEqual(File.ReadAllBytes(target)) Then
                                ' Испорченный файл с тем же именем не принимаем за проверенный.
                                AtomicFile.Write(target, Sub(stream) stream.Write(bytes, 0, bytes.Length), backupPath:=AtomicFile.RejectedPath(target))
                            End If
                            paths.Add(source, relative)
                        End If
                        style.SetAsset(i, paths(source))
                    Next
                Next
                Dim absolute = Clone(candidate)
                For Each style In absolute.Styles()
                    Dim assets = style.Assets()
                    For i = 0 To assets.Length - 1
                        If assets(i) <> "" Then style.SetAsset(i, ResolveMedia(folder, assets(i)))
                    Next
                Next
                ready = New LoadedAppearance(absolute)
                Dim config = IO.Path.Combine(folder, "config.xml")
                Dim previous = config & ".bak"
                If File.Exists(config) Then
                    Try
                        Open(config)
                    Catch ex As Exception
                        previous = AtomicFile.RejectedPath(config)
                    End Try
                End If
                AtomicFile.Write(config, Sub(stream) Serializer.Serialize(stream, candidate), backupPath:=previous)
                committed = True
                Return ready
            Finally
                If Not committed Then
                    If ready IsNot Nothing Then ready.Dispose()
                    For Each item In created
                        Try
                            If File.Exists(item) Then File.Delete(item)
                        Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                            Diagnostics.Debug.WriteLine(ex.Message)
                        End Try
                    Next
                End If
            End Try
        End Using
    End Function
End Module

Public NotInheritable Class AppearanceService
    Implements IDisposable
    Public Event Changed As EventHandler
    Public ReadOnly Property Current As LoadedAppearance
    Public ReadOnly Property Folder As String
    Public ReadOnly Property StartupError As String = ""

    Public Sub New(folder As String, Optional defaultScreen As Integer = 0)
        Me.Folder = IO.Path.GetFullPath(folder)
        Dim defaults As New AppearanceProfile With {.ScreenIndex = defaultScreen}
        Try
            Dim path = AppearanceStorage.ConfigPath(Me.Folder)
            _Current = New LoadedAppearance(If(File.Exists(path), AppearanceStorage.Open(path), defaults))
        Catch ex As Exception
            _Current = New LoadedAppearance(defaults)
            _StartupError = "Не удалось загрузить оформление. Используется встроенное. " & ex.Message
        End Try
    End Sub

    Public Sub Apply(profile As AppearanceProfile)
        Dim replacement = AppearanceStorage.Save(profile, Folder)
        Dim previous = Current
        _Current = replacement
        ' Все открытые табло получают новые ссылки прежде, чем старые Bitmap освобождены.
        Try
            RaiseEvent Changed(Me, EventArgs.Empty)
        Finally
            previous.Dispose()
        End Try
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Current.Dispose()
    End Sub
End Class
