Option Strict On
Imports System.IO
Imports System.Xml
Imports System.Xml.Linq
Imports System.Xml.Serialization

' Конструкторы моделей задают значения для нового матча. При чтении XML они
' не должны скрывать отсутствующие части сохранения: сначала проверяем структуру.
Public Module SavedMatchReader
    Private ReadOnly Serializer As New XmlSerializer(GetType(SavedMatch))

    Public Function Read(path As String) As SavedMatch
        Dim document As XDocument
        Dim settings As New XmlReaderSettings With {.DtdProcessing = DtdProcessing.Prohibit, .XmlResolver = Nothing, .MaxCharactersInDocument = 10000000}
        Try
            Using reader = XmlReader.Create(path, settings)
                document = XDocument.Load(reader)
            End Using
        Catch ex As XmlException
            Throw New InvalidOperationException("Не удалось прочитать XML сохранения: " & ex.Message, ex)
        End Try
        Dim root = document.Root
        If root Is Nothing OrElse root.Name <> XName.Get("SavedMatch") Then Throw New InvalidDataException("Неверный корень сохранения матча.")
        RejectNil(root)
        CheckSnapshot(Required(root, "Current"))
        Dim history = Required(root, "History")
        If history.Elements().Any(Function(entry) entry.Name <> XName.Get("UndoEntry")) OrElse history.Elements().Count() > 100 Then Throw New InvalidDataException("Неверная история действий.")
        For Each entry In history.Elements("UndoEntry")
            RejectNil(entry)
            Required(entry, "Description")
            CheckSnapshot(Required(entry, "State"))
        Next
        Dim loaded As SavedMatch
        Using reader = document.CreateReader()
            loaded = DirectCast(Serializer.Deserialize(reader), SavedMatch)
        End Using
        If loaded Is Nothing OrElse loaded.Current Is Nothing OrElse loaded.History Is Nothing Then Throw New InvalidDataException("Неполное сохранение матча.")
        GameRules.UpgradeLegacy(loaded.Current)
        GameRules.Validate(loaded.Current)
        For Each entry In loaded.History
            If entry Is Nothing OrElse entry.Description Is Nothing Then Throw New InvalidDataException("Неверная история действий.")
            GameRules.UpgradeLegacy(entry.State)
            GameRules.Validate(entry.State)
        Next
        Return loaded
    End Function

    Private Sub CheckSnapshot(snapshot As XElement)
        Dim version As Integer
        If Not Integer.TryParse(Required(snapshot, "FormatVersion").Value, Globalization.NumberStyles.Integer, Globalization.CultureInfo.InvariantCulture, version) OrElse (version <> 1 AndAlso version <> 2) Then Throw New InvalidDataException("Неподдерживаемое сохранение игры.")
        Dim main = Required(snapshot, "Main")
        For Each name In {"LeftScore", "RightScore", "Bank", "BankId", "Round"}
            Required(main, name)
        Next
        CheckAnswers(Required(main, "Answers"), 6)
        For Each name In {"LeftStrikes", "RightStrikes"}
            Dim strikes = Required(main, name)
            If strikes.Elements().Count() <> 3 OrElse strikes.Elements("boolean").Count() <> 3 Then Throw New InvalidDataException("Неверные маркеры промахов.")
        Next
        If version = 1 Then
            Required(main, "Splash")
        Else
            Dim splash = Required(main, "SplashScreen")
            For Each name In {"Visible", "Line1", "Line2"}
                Required(splash, name)
            Next
        End If
        Dim big = Required(snapshot, "Big")
        For Each name In {"ScoreAdjustment", "HideFirstPlayer", "TargetScore"}
            Required(big, name)
        Next
        CheckAnswers(Required(big, "Answers"), 10)
    End Sub

    Private Sub CheckAnswers(container As XElement, count As Integer)
        If container.Elements().Count() <> count OrElse container.Elements("AnswerState").Count() <> count Then Throw New InvalidDataException("Неполный список ответов.")
        For Each answer In container.Elements("AnswerState")
            For Each name In {"Text", "Points", "TextVisible", "ScoreVisible", "AwardedPoints", "AwardedBank"}
                Required(answer, name)
            Next
        Next
    End Sub

    Private Function Required(parent As XElement, name As String) As XElement
        Dim matches = parent.Elements(name).ToArray()
        If matches.Length <> 1 Then Throw New InvalidDataException("Сохранение: требуется один элемент «" & name & "» в «" & parent.Name.LocalName & "».")
        Dim element = matches(0)
        RejectNil(element)
        Return element
    End Function

    Private Sub RejectNil(element As XElement)
        Dim nil = CStr(element.Attribute(XName.Get("nil", "http://www.w3.org/2001/XMLSchema-instance")))
        If nil = "true" OrElse nil = "1" Then Throw New InvalidDataException("Сохранение: элемент «" & element.Name.LocalName & "» не может быть пустой ссылкой.")
    End Sub
End Module
