Option Strict On
Imports System.Globalization
Imports System.IO
Imports System.Text

Public Class QuestionBank
    Public Property Rounds As AnswerState()()
End Class

Public Module QuestionBankReader
    Public Function ReadFile(path As String) As QuestionBank
        Return Parse(File.ReadAllText(path, New UTF8Encoding(False, True)))
    End Function

    Public Function Parse(text As String) As QuestionBank
        Dim lines As New List(Of String)()
        Using reader As New StringReader(text)
            While reader.Peek() >= 0
                Dim line = reader.ReadLine()
                If Not String.IsNullOrWhiteSpace(line) Then lines.Add(line.Trim())
            End While
        End Using
        If lines.Count <> 24 Then Throw New InvalidDataException("В базе должно быть 24 непустые строки; найдено " & lines.Count & ".")
        Dim rounds(3)() As AnswerState
        Dim reversePoints = {15, 30, 45, 60, 120, 240}
        For roundIndex = 0 To 3
            rounds(roundIndex) = MainGameState.CreateAnswers(6)
            For index = 0 To 5
                Dim lineNumber = roundIndex * 6 + index
                Dim answer = lines(lineNumber)
                Dim points = reversePoints(index)
                If roundIndex < 3 Then
                    Dim parts = answer.Split("&"c)
                    If parts.Length <> 2 OrElse Not Integer.TryParse(parts.Last(), NumberStyles.None, CultureInfo.InvariantCulture, points) Then Throw New InvalidDataException("Строка " & (lineNumber + 1) & ": ожидается Ответ&целое число.")
                    answer = parts(0).Trim()
                End If
                Try
                    GameRules.CheckAnswer(answer, points)
                Catch ex As InvalidOperationException
                    Throw New InvalidDataException("Строка " & (lineNumber + 1) & ": " & ex.Message, ex)
                End Try
                rounds(roundIndex)(index) = New AnswerState With {.Text = answer, .Points = points}
            Next
        Next
        Return New QuestionBank With {.Rounds = rounds}
    End Function
End Module
