Option Strict On
Imports System.IO
Imports System.Xml.Serialization

' Сторона команды едина для пульта и табло: вместо прежних строк "L"/"R",
' которые в обработчиках ручной правки счёта оказались перепутаны.
Public Enum TeamSide
    Left
    Right
End Enum

' Данные ответа, не зависящие от Label/TextBox и цвета кнопки.
' В большой игре текст и цена раскрываются отдельно; в основной — вместе.
Public Class AnswerState
    Public Property Text As String = ""
    Public Property Points As Integer
    Public Property TextVisible As Boolean
    Public Property ScoreVisible As Boolean
    ' Фактически начисленная сумма с множителем; она не обязана совпадать с Points.
    Public Property AwardedPoints As Integer
    ' -1 — очки ещё не начислялись. Иначе это номер банка, в который они попали.
    ' Признак сохраняется после скрытия строки, чтобы повторный показ не дал очки дважды.
    Public Property AwardedBank As Integer = -1
End Class

Public Class MainGameState
    Public Property LeftScore As Integer
    Public Property RightScore As Integer
    Public Property Bank As Integer
    ' После передачи команде начинается новый банк с новым номером.
    ' Это позволяет отличать текущие начисления от очков, уже переданных команде.
    Public Property BankId As Integer
    Public Property Round As Integer = 1
    ' -1 — игровое поле; 0..5 — заставки. Заставка не стирает ответы и счёт.
    Public Property Splash As Integer = 0
    Public Property Answers As AnswerState() = CreateAnswers(6)
    Public Property LeftStrikes As Boolean() = New Boolean(2) {}
    Public Property RightStrikes As Boolean() = New Boolean(2) {}

    ' Множитель вычисляется из раунда: отдельная радиокнопка не может с ним разойтись.
    <XmlIgnore>
    Public ReadOnly Property Multiplier As Integer
        Get
            Return If(Round = 2 OrElse Round = 3, Round, 1)
        End Get
    End Property

    Public Shared Function CreateAnswers(count As Integer) As AnswerState()
        ' Каждая строка получает собственный объект, иначе правка одной затронула бы остальные.
        Return Enumerable.Range(0, count).Select(Function(i) New AnswerState()).ToArray()
    End Function
End Class

Public Class BigGameState
    Public Property Answers As AnswerState() = MainGameState.CreateAnswers(10)
    ' Ручная поправка хранится отдельно от суммы открытых цен. Последующие правки
    ' конкретных вопросов сохраняют эту поправку, вместо перезаписи общего итога.
    Public Property ScoreAdjustment As Integer
    Public Property HideFirstPlayer As Boolean
    ' Сохраняем прежнее правило мероприятия: победа с 150 очков по умолчанию.
    Public Property TargetScore As Integer = 150

    <XmlIgnore>
    Public ReadOnly Property Total As Integer
        Get
            ' Присваивание новой цены уже открытой строке само изменяет итог на разницу.
            Return ScoreAdjustment + Answers.Where(Function(a) a.ScoreVisible).Sum(Function(a) a.Points)
        End Get
    End Property

    <XmlIgnore>
    Public ReadOnly Property NextStep As Integer
        Get
            ' 0..19: чётный шаг открывает текст, нечётный — цену; 20 означает завершение.
            ' Отдельного счётчика нет: отмена и прямой показ строки не рассинхронизируют шаги.
            For i = 0 To Answers.Length - 1
                If Not Answers(i).TextVisible Then Return i * 2
                If Not Answers(i).ScoreVisible Then Return i * 2 + 1
            Next
            Return 20
        End Get
    End Property
End Class

Public Class MatchState
    ' Проверяется при восстановлении: будущие несовместимые форматы нельзя читать молча.
    Public Property FormatVersion As Integer = 1
    Public Property Main As New MainGameState()
    Public Property Big As New BigGameState()
End Class

' Правила изменяют переданный снимок. Публикация, история и сохранение — в GameSession.
' Здесь нет окон, звуков или USB: расчёты можно проверить без запуска интерфейса.
Public Module GameRules
    Public Const MaximumScore As Integer = 999999
    Public Const MaximumAnswerPoints As Integer = 999

    Public Sub CheckScore(value As Integer, Optional name As String = "Счёт")
        If value < 0 OrElse value > MaximumScore Then Throw New InvalidOperationException(name & ": допустимо от 0 до " & MaximumScore & ".")
    End Sub

    Public Sub CheckAnswer(text As String, points As Integer)
        If String.IsNullOrWhiteSpace(text) Then Throw New InvalidOperationException("Введите текст ответа.")
        If text.Length > 120 Then Throw New InvalidOperationException("Ответ должен содержать не более 120 символов.")
        If points < 0 OrElse points > MaximumAnswerPoints Then Throw New InvalidOperationException("Очки ответа: допустимо от 0 до " & MaximumAnswerPoints & ".")
    End Sub

    Public Sub Validate(state As MatchState)
        ' Одна проверка применяется и к новой команде, и к состояниям из файла сохранения.
        ' Непоказанный ответ может быть пустым черновиком; показанный — уже нет.
        If state Is Nothing OrElse state.FormatVersion <> 1 OrElse state.Main Is Nothing OrElse state.Big Is Nothing Then Throw New InvalidDataException("Неподдерживаемое сохранение игры.")
        Dim main = state.Main
        If main.Answers Is Nothing OrElse main.Answers.Length <> 6 OrElse main.LeftStrikes Is Nothing OrElse main.LeftStrikes.Length <> 3 OrElse main.RightStrikes Is Nothing OrElse main.RightStrikes.Length <> 3 Then Throw New InvalidDataException("Неверное состояние основной игры.")
        If main.Round < 1 OrElse main.Round > 4 OrElse main.Splash < -1 OrElse main.Splash > 5 OrElse main.BankId < 0 Then Throw New InvalidDataException("Неверный раунд.")
        CheckScore(main.LeftScore) : CheckScore(main.RightScore) : CheckScore(main.Bank)
        If state.Big.Answers Is Nothing OrElse state.Big.Answers.Length <> 10 Then Throw New InvalidDataException("Неверное состояние большой игры.")
        For Each answer In main.Answers.Concat(state.Big.Answers)
            If answer Is Nothing OrElse answer.Text Is Nothing OrElse answer.Text.Length > 120 OrElse answer.Points < 0 OrElse answer.Points > MaximumAnswerPoints Then Throw New InvalidDataException("Неверный ответ в сохранении.")
            If answer.TextVisible Then CheckAnswer(answer.Text, answer.Points)
            If answer.ScoreVisible AndAlso Not answer.TextVisible Then Throw New InvalidDataException("Очки не могут быть открыты раньше ответа.")
            If answer.AwardedPoints < 0 OrElse answer.AwardedPoints > MaximumAnswerPoints * 3 OrElse answer.AwardedBank < -1 OrElse answer.AwardedBank > main.BankId Then Throw New InvalidDataException("Неверное начисление в сохранении.")
        Next
        If state.Big.ScoreAdjustment < -MaximumScore OrElse state.Big.ScoreAdjustment > MaximumScore Then Throw New InvalidDataException("Неверная коррекция итога.")
        CheckScore(state.Big.Total)
        If state.Big.TargetScore < 1 OrElse state.Big.TargetScore > MaximumScore Then Throw New InvalidDataException("Неверная цель большой игры.")
    End Sub

    Public Function Clone(state As MatchState) As MatchState
        ' XML-копирование отделяет вложенные массивы и ответы от живой игры и истории Undo.
        ' Это простой вариант для небольшой модели, совместимый с форматом автосохранения.
        Using stream As New MemoryStream()
            Dim serializer As New XmlSerializer(GetType(MatchState))
            serializer.Serialize(stream, state)
            stream.Position = 0
            Return DirectCast(serializer.Deserialize(stream), MatchState)
        End Using
    End Function

    Public Sub EditMainAnswer(state As MainGameState, index As Integer, text As String, points As Integer)
        CheckAnswer(text, points)
        Dim answer = state.Answers(index)
        If answer.AwardedBank >= 0 AndAlso answer.Points <> points Then
            ' После передачи банка не пересчитываем командный счёт задним числом.
            ' Оператор сначала отменяет передачу, затем правит цену и передаёт банк снова.
            If answer.AwardedBank <> state.BankId Then Throw New InvalidOperationException("Очки уже переданы команде. Сначала отмените передачу банка, затем исправьте цену ответа.")
            Dim newAward = points * state.Multiplier
            ' Убираем старое начисление и добавляем новое, а не всю новую цену повторно.
            state.Bank += newAward - answer.AwardedPoints
            CheckScore(state.Bank, "Банк после исправления")
            answer.AwardedPoints = newAward
        End If
        answer.Text = text.Trim()
        answer.Points = points
    End Sub

    Public Sub ShowMainAnswer(state As MainGameState, index As Integer, text As String, points As Integer, award As Boolean)
        EditMainAnswer(state, index, text, points)
        Dim answer = state.Answers(index)
        answer.TextVisible = True
        answer.ScoreVisible = True
        state.Splash = -1
        ' Открытие ранее скрытого начисленного ответа меняет только показ.
        ' Впервые открытый без начисления ответ допускает последующее начисление один раз.
        If award AndAlso answer.AwardedBank = -1 Then
            answer.AwardedPoints = answer.Points * state.Multiplier
            answer.AwardedBank = state.BankId
            state.Bank += answer.AwardedPoints
            CheckScore(state.Bank, "Банк")
        End If
    End Sub

    Public Sub TransferBank(state As MainGameState, side As TeamSide)
        If state.Bank = 0 Then Throw New InvalidOperationException("Банк пуст: передавать нечего.")
        If side = TeamSide.Left Then
            state.LeftScore += state.Bank
            CheckScore(state.LeftScore)
        Else
            state.RightScore += state.Bank
            CheckScore(state.RightScore)
        End If
        state.Bank = 0
        ' Сами признаки начисления в ответах сохраняются: перенос не разрешает начислить их снова.
        state.BankId += 1
    End Sub

    Public Sub ResetRound(state As MainGameState)
        ' Сбрасываем одну модель целиком, а не только цифры зрительского окна.
        ' Счета команд, тексты и базовые цены намеренно остаются для повторной подготовки.
        state.Bank = 0
        state.BankId = 0
        state.Splash = -1
        state.LeftStrikes = New Boolean(2) {}
        state.RightStrikes = New Boolean(2) {}
        For Each answer In state.Answers
            answer.TextVisible = False
            answer.ScoreVisible = False
            answer.AwardedPoints = 0
            answer.AwardedBank = -1
        Next
    End Sub

    Public Sub ShowBigText(state As BigGameState, index As Integer, text As String)
        CheckAnswer(text, state.Answers(index).Points)
        state.Answers(index).Text = text.Trim()
        state.Answers(index).TextVisible = True
    End Sub

    Public Sub ShowBigPoints(state As BigGameState, index As Integer, points As Integer)
        Dim answer = state.Answers(index)
        If Not answer.TextVisible Then Throw New InvalidOperationException("Сначала покажите текст этого ответа.")
        CheckAnswer(answer.Text, points)
        answer.Points = points
        answer.ScoreVisible = True
        ' Total вычисляется из открытых цен, поэтому повторная правка не дублирует начисление.
        CheckScore(state.Total, "Итог после исправления")
    End Sub

    Public Sub SetBigTotal(state As BigGameState, total As Integer)
        CheckScore(total)
        ' Например, сумма ответов 100, желаемый итог 120: сохраняем поправку +20.
        state.ScoreAdjustment = total - state.Answers.Where(Function(a) a.ScoreVisible).Sum(Function(a) a.Points)
    End Sub
End Module
