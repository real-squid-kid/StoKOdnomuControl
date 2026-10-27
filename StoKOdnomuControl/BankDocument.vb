Option Strict On
Imports System.IO
Imports System.Security.Cryptography
Imports System.Text

' Один источник правил для пульта, чтения TXT и вычислений редактора.
Public Module RoundRules
    Public Function Multiplier(round As Integer) As Integer
        Return If(round >= 1 AndAlso round <= 3, round, 1)
    End Function

    Public Function ReversePrices() As Integer()
        Return New Integer() {15, 30, 45, 60, 120, 240}
    End Function
End Module

Public Module AtomicFile
    ' Замена выполняется в том же каталоге. Ошибка оставляет прежний файл и .bak;
    ' временный файл никогда не становится выбранным документом раньше проверки.
    Public Sub Write(path As String, bytes As Byte())
        Dim target = IO.Path.GetFullPath(path)
        Dim temporary = target & "." & Guid.NewGuid().ToString("N") & ".tmp"
        Try
            Using stream As New FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)
                stream.Write(bytes, 0, bytes.Length)
                stream.Flush(True)
            End Using
            If Not bytes.SequenceEqual(File.ReadAllBytes(temporary)) Then Throw New IOException("Не удалось проверить записанный файл.")
            If File.Exists(target) Then
                File.Replace(temporary, target, target & ".bak", True)
            Else
                File.Move(temporary, target)
            End If
        Finally
            If File.Exists(temporary) Then File.Delete(temporary)
        End Try
    End Sub
End Module

Public Class BankConflictException
    Inherits IOException
    Public Sub New()
        MyBase.New("Файл базы изменён другим приложением. Перечитайте его или сохраните под другим именем.")
    End Sub
End Class

' Файловый документ не владеет игровой сессией: сохранить TXT не значит начать раунд.
Public NotInheritable Class BankDocument
    Public Property Bank As QuestionBank = EmptyBank()
    Public Property FilePath As String = ""
    Private _fingerprint As String = ""

    Public Shared Function EmptyBank() As QuestionBank
        Dim rounds(3)() As AnswerState
        For r = 0 To 3
            rounds(r) = MainGameState.CreateAnswers(6)
            For i = 0 To 5
                If r = 3 Then rounds(r)(i).Points = RoundRules.ReversePrices()(i)
            Next
        Next
        Return New QuestionBank With {.Rounds = rounds}
    End Function

    Public Shared Function Open(path As String) As BankDocument
        Dim bytes = File.ReadAllBytes(path)
        Dim text = New UTF8Encoding(False, True).GetString(bytes).TrimStart(ChrW(&HFEFF))
        Return New BankDocument With {.Bank = QuestionBankReader.Parse(text), .FilePath = IO.Path.GetFullPath(path), ._fingerprint = Fingerprint(bytes)}
    End Function

    Public Shared Function Serialize(bank As QuestionBank) As String
        If bank Is Nothing OrElse bank.Rounds Is Nothing OrElse bank.Rounds.Length <> 4 Then Throw New InvalidDataException("В базе нужны четыре раунда.")
        Dim lines As New List(Of String)()
        For r = 0 To 3
            If bank.Rounds(r) Is Nothing OrElse bank.Rounds(r).Length <> 6 Then Throw New InvalidDataException("В каждом раунде нужны шесть ответов.")
            For i = 0 To 5
                Dim answer = bank.Rounds(r)(i)
                Try
                    If answer Is Nothing Then Throw New InvalidOperationException("Ответ не задан.")
                    GameRules.CheckAnswer(answer.Text, answer.Points)
                    If answer.Text.IndexOfAny(New Char() {ChrW(13), ChrW(10)}) >= 0 Then Throw New InvalidOperationException("Ответ должен занимать одну строку.")
                    If r < 3 AndAlso answer.Text.Contains("&") Then Throw New InvalidOperationException("Символ & используется как разделитель; уберите его из ответа.")
                    If r = 3 AndAlso answer.Points <> RoundRules.ReversePrices()(i) Then Throw New InvalidOperationException("Цена игры наоборот фиксирована.")
                Catch ex As InvalidOperationException
                    Throw New InvalidDataException("Раунд «" & RoundName(r) & "», строка " & (i + 1) & ": " & ex.Message, ex)
                End Try
                lines.Add(answer.Text.Trim() & If(r < 3, "&" & answer.Points.ToString(Globalization.CultureInfo.InvariantCulture), ""))
            Next
        Next
        Dim text = String.Join(vbCrLf, lines) & vbCrLf
        QuestionBankReader.Parse(text)
        Return text
    End Function

    Public Shared Function RoundName(index As Integer) As String
        Return New String() {"Простая", "Двойная", "Тройная", "Наоборот"}(index)
    End Function

    Public Sub Save(path As String, candidate As QuestionBank)
        Dim target = IO.Path.GetFullPath(path)
        Dim content = Serialize(candidate)
        If String.Equals(target, FilePath, StringComparison.OrdinalIgnoreCase) Then
            If Not File.Exists(target) OrElse Fingerprint(File.ReadAllBytes(target)) <> _fingerprint Then Throw New BankConflictException()
        End If
        Dim bytes = New UTF8Encoding(False).GetBytes(content)
        AtomicFile.Write(target, bytes)
        ' Путь, контрольная сумма и модель меняются только после успешной записи.
        Bank = QuestionBankReader.Parse(content)
        FilePath = target
        _fingerprint = Fingerprint(bytes)
    End Sub

    Private Shared Function Fingerprint(bytes As Byte()) As String
        Using hash = SHA256.Create()
            Return Convert.ToBase64String(hash.ComputeHash(bytes))
        End Using
    End Function
End Class
