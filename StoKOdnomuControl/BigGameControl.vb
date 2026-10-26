Option Strict On
Imports System.Windows.Forms

' Пульт большой игры использует ту же GameSession, что и основной пульт.
' Индексы 0..4 — первый участник, 5..9 — второй; цена и текст раскрываются отдельно.
Public Class BigGameControl
    Inherits Form
    Private ReadOnly _session As GameSession
    Private ReadOnly _countdown As Countdown
    Private ReadOnly _undoAction As Action
    Private ReadOnly _texts(9) As TextBox
    Private ReadOnly _points(9) As NumericUpDown
    Private ReadOnly _textButtons(9) As Button
    Private ReadOnly _pointButtons(9) As Button
    Private ReadOnly _hide As New CheckBox With {.Text = "Скрыть ответы первого участника на табло", .AutoSize = True, .Margin = New Padding(6, 12, 4, 4)}
    Private ReadOnly _total As NumericUpDown = OperatorUi.Number()
    Private ReadOnly _target As NumericUpDown = OperatorUi.Number()
    Private ReadOnly _timerStatus As Label = OperatorUi.Label("Таймер остановлен", 260)
    Private ReadOnly _actionStatus As Label = OperatorUi.Label("", 1200)
    Private _next As Button
    Private _undo As Button
    Private _last As BigGameState
    ' Изменение Checked во время рендера не должно создать новую команду Undo.
    Private _updating As Boolean

    Public Sub New(session As GameSession, countdown As Countdown, showBoard As Action, undoAction As Action)
        ' Зависимости передаются владельцем: второй пульт не создаёт свой матч или отсчёт.
        _session = session
        _countdown = countdown
        _undoAction = undoAction
        BuildUi(showBoard)
        AddHandler _session.Changed, AddressOf SessionChanged
        AddHandler countdown.Changed, AddressOf CountdownChanged
        AddHandler FormClosing, Sub(sender, e)
                                    If e.CloseReason = CloseReason.UserClosing Then
                                        e.Cancel = True
                                        Hide()
                                    End If
                                End Sub
        AddHandler Disposed, Sub()
                                 RemoveHandler _session.Changed, AddressOf SessionChanged
                                 RemoveHandler countdown.Changed, AddressOf CountdownChanged
                             End Sub
        KeyPreview = True
        AddHandler KeyDown, Sub(sender, e)
                                If e.Control AndAlso e.KeyCode = Keys.Z Then
                                    _undoAction()
                                    e.SuppressKeyPress = True
                                End If
                            End Sub
        SessionChanged(Me, EventArgs.Empty)
    End Sub

    Private Sub BuildUi(showBoard As Action)
        ' Вместо прежнего Designer: два столбца участников и одинаковые действия пяти вопросов.
        ' Надписи кнопок меняются со стадией раскрытия, а не с цветом контрола.
        Dim page = OperatorUi.Page(Me, "Сто к одному — большая игра")
        OperatorUi.Heading(page, "Большая игра · два участника, пять вопросов")
        Dim actions = OperatorUi.Row(page)
        _next = OperatorUi.Button(actions, "", AddressOf NextStep, 360)
        _next.Name = "RevealNext"
        _undo = OperatorUi.Button(actions, "Отменить (Ctrl+Z)", Sub() _undoAction(), 200)
        OperatorUi.Button(actions, "Показать табло", showBoard, 180)
        OperatorUi.Button(actions, "Новая большая игра…", AddressOf ResetGame, 230)
        Dim timer = OperatorUi.Row(page)
        OperatorUi.Button(timer, "Начать / заново · 20 с", Sub() _countdown.Start(20), 220)
        OperatorUi.Button(timer, "Начать / заново · 25 с", Sub() _countdown.Start(25), 220)
        OperatorUi.Button(timer, "Остановить таймер и трек", Sub() _countdown.Stop(), 250)
        OperatorUi.Button(timer, "Звук повтора ответа", Sub() AudioService.Instance.Play(SoundCue.SameAnswer), 210)
        timer.Controls.Add(_timerStatus)
        Dim scores = OperatorUi.Row(page)
        scores.Controls.Add(OperatorUi.Label("Общий итог:", 115))
        _total.Name = "BigTotal"
        scores.Controls.Add(_total)
        OperatorUi.Button(scores, "Исправить общий итог", Sub() Run("Исправить итог большой игры", Sub(state) GameRules.SetBigTotal(state.Big, CInt(_total.Value))), 215)
        scores.Controls.Add(OperatorUi.Label("Цель для победы:", 155))
        _target.Minimum = 1
        scores.Controls.Add(_target)
        OperatorUi.Button(scores, "Применить цель", Sub() Run("Изменить цель большой игры", Sub(state) state.Big.TargetScore = CInt(_target.Value)), 165)
        scores.Controls.Add(_hide)
        AddHandler _hide.CheckedChanged, Sub()
                                            If Not _updating Then _session.Execute(If(_hide.Checked, "Скрыть", "Вернуть") & " ответы первого участника", Sub(state) state.Big.HideFirstPlayer = _hide.Checked)
                                        End Sub
        Dim players = OperatorUi.Grid(page, 0, 0)
        For player = 0 To 1
            Dim columnPage As New FlowLayoutPanel With {.AutoSize = True, .FlowDirection = FlowDirection.TopDown, .WrapContents = False, .Dock = DockStyle.Fill, .Margin = New Padding(2)}
            OperatorUi.Cell(players, columnPage, player, 0)
            Dim title = OperatorUi.Label("Участник " & (player + 1), 550)
            title.Font = New Font("Segoe UI", 12, FontStyle.Bold)
            columnPage.Controls.Add(title)
            Dim table = OperatorUi.Grid(columnPage, 40, 0, 65, 130, 145)
            table.Width = 575
            table.Dock = DockStyle.Top
            Dim headings = {"№", "Ответ (черновик)", "Очки", "Текст на табло", "Очки в общий итог"}
            For column = 0 To headings.Length - 1
                OperatorUi.Cell(table, OperatorUi.Label(headings(column), If(column = 1, 165, CInt(Math.Max(30, table.ColumnStyles(column).Width - 8)))), column, 0)
            Next
            For question = 0 To 4
                Dim index = player * 5 + question
                ' Замыкание получает отдельный index для этой строки обоих участников.
                _texts(index) = New TextBox With {.Name = "BigAnswer" & index, .MaxLength = 120, .Margin = New Padding(4, 10, 4, 4)}
                _points(index) = OperatorUi.Number(GameRules.MaximumAnswerPoints)
                _points(index).Width = 58
                _points(index).Name = "BigPoints" & index
                OperatorUi.Cell(table, OperatorUi.Label((question + 1).ToString(), 30), 0, question + 1)
                OperatorUi.Cell(table, _texts(index), 1, question + 1)
                OperatorUi.Cell(table, _points(index), 2, question + 1)
                _textButtons(index) = OperatorUi.Button(table, "Показать текст", Sub() ShowText(index), 122)
                _textButtons(index).Name = "ShowBigText" & index
                table.SetCellPosition(_textButtons(index), New TableLayoutPanelCellPosition(3, question + 1))
                _pointButtons(index) = OperatorUi.Button(table, "Начислить", Sub() ShowPoints(index), 137)
                _pointButtons(index).Name = "ShowBigPoints" & index
                table.SetCellPosition(_pointButtons(index), New TableLayoutPanelCellPosition(4, question + 1))
            Next
        Next
        Dim note = OperatorUi.Label("Правка очков уже открытого ответа меняет итог на разницу. «Отменить» возвращает последнее игровое действие в любом пульте. Таймер останавливается отдельно.", 1200)
        note.Height = 48
        page.Controls.Add(note)
        Dim dimFields As New CheckBox With {.Text = "Приглушить ответы и цены в пульте", .AutoSize = True, .Margin = New Padding(6)}
        page.Controls.Add(dimFields)
        AddHandler dimFields.CheckedChanged, Sub()
                                                 For i = 0 To 9
                                                     _texts(i).ForeColor = If(dimFields.Checked, Color.LightGray, SystemColors.WindowText)
                                                     _points(i).ForeColor = If(dimFields.Checked, Color.LightGray, SystemColors.WindowText)
                                                 Next
                                             End Sub
        Dim drafts = OperatorUi.Row(page)
        OperatorUi.Button(drafts, "Сохранить черновики ответов", AddressOf SaveDrafts, 285)
        drafts.Controls.Add(OperatorUi.Label("Черновики сохраняются без раскрытия и начисления очков.", 650))
        _actionStatus.Height = 48
        page.Controls.Add(_actionStatus)
    End Sub

    Private Sub NextStep()
        ' Берём первый ещё не раскрытый элемент из модели. Нет отдельного State += 1,
        ' который раньше пропускал вопрос при ошибке или отставал после ручной правки.
        Dim stepNumber = _session.State.Big.NextStep
        If stepNumber >= 20 Then Return
        Dim index = stepNumber \ 2
        If stepNumber Mod 2 = 0 Then
            ShowText(index)
        Else
            ShowPoints(index)
        End If
    End Sub

    Private Sub ShowText(index As Integer)
        Dim wasVisible = _session.State.Big.Answers(index).TextVisible
        Run(If(wasVisible, "Исправить", "Показать") & " текст: " & AnswerName(index), Sub(state) GameRules.ShowBigText(state.Big, index, _texts(index).Text))
        ' Только первое раскрытие озвучивается; коррекция текста не имитирует новый ответ.
        If Not wasVisible Then AudioService.Instance.Play(CType(CInt(SoundCue.Reveal1) + index Mod 5, SoundCue))
    End Sub

    Private Sub ShowPoints(index As Integer)
        Dim before = _session.State.Big
        Run(If(before.Answers(index).ScoreVisible, "Исправить", "Начислить") & " очки: " & AnswerName(index), Sub(state) GameRules.ShowBigPoints(state.Big, index, CInt(_points(index).Value)))
        Dim after = _session.State.Big
        ' Победный эффект связан с переходом через цель, а не с каждым рендером итога.
        ' Undo не проходит через этот обработчик и не проигрывает победу заново.
        If before.Total < after.TargetScore AndAlso after.Total >= after.TargetScore Then
            AudioService.Instance.Play(SoundCue.Victory)
        Else
            AudioService.Instance.Play(If(CInt(_points(index).Value) = 0, SoundCue.BigWrong, SoundCue.OpenAnswer))
        End If
    End Sub

    Private Sub Run(description As String, action As Action(Of MatchState))
        _session.Execute(description, action)
    End Sub

    Private Shared Function AnswerName(index As Integer) As String
        Return "участник " & (index \ 5 + 1) & ", вопрос " & (index Mod 5 + 1)
    End Function

    Private Sub SaveDrafts()
        ' Сохраняем только непоказанные строки. Опубликованные ответы меняются отдельными
        ' кнопками, чтобы эта команда не исправила счёт/экран незаметно.
        _session.Execute("Сохранить черновики большой игры", Sub(state)
                                                                  For i = 0 To 9
                                                                      Dim answer = state.Big.Answers(i)
                                                                      If answer.TextVisible OrElse answer.ScoreVisible Then Continue For
                                                                      If Not String.IsNullOrWhiteSpace(_texts(i).Text) Then GameRules.CheckAnswer(_texts(i).Text, CInt(_points(i).Value))
                                                                      answer.Text = _texts(i).Text.Trim()
                                                                      answer.Points = CInt(_points(i).Value)
                                                                  Next
                                                              End Sub)
    End Sub

    Private Sub ResetGame()
        ' Сбрасываем публикацию и ручную поправку, сохраняя выбранную цель победы.
        If Not OperatorUi.Confirm(Me, "Очистить все ответы и итог большой игры? Цель для победы сохранится.") Then Return
        ReloadDrafts()
        _session.Execute("Новая большая игра", Sub(state) state.Big = New BigGameState With {.TargetScore = state.Big.TargetScore})
        _countdown.Stop()
    End Sub

    Friend Sub ReloadDrafts()
        ' Явное восстановление/новый матч требуют замены старого ввода при следующем рендере.
        _last = Nothing
    End Sub

    Private Sub SessionChanged(sender As Object, e As EventArgs)
        ' Защита от обратного вызова CheckedChanged и стирания несвязанных черновиков.
        ' Доступность начисления определяется опубликованным текстом конкретной строки.
        _updating = True
        Dim state = _session.State.Big
        For i = 0 To 9
            Dim answer = state.Answers(i)
            If _last Is Nothing OrElse _last.Answers(i).Text <> answer.Text Then _texts(i).Text = answer.Text
            If _last Is Nothing OrElse _last.Answers(i).Points <> answer.Points Then _points(i).Value = answer.Points
            _textButtons(i).Text = If(answer.TextVisible, "Исправить текст", "Показать текст")
            _pointButtons(i).Text = If(answer.ScoreVisible, "Исправить очки", "Начислить")
            _pointButtons(i).Enabled = answer.TextVisible
        Next
        If _last Is Nothing OrElse _last.Total <> state.Total Then _total.Value = state.Total
        If _last Is Nothing OrElse _last.TargetScore <> state.TargetScore Then _target.Value = state.TargetScore
        _hide.Checked = state.HideFirstPlayer
        Dim nextStep = state.NextStep
        _next.Enabled = nextStep < 20
        _next.Text = If(nextStep >= 20, "Все ответы и очки открыты", "Далее: " & If(nextStep Mod 2 = 0, "текст", "очки") & " · " & AnswerName(Math.Min(9, nextStep \ 2)))
        _undo.Enabled = _session.UndoDescription <> ""
        _actionStatus.Text = "Итог: " & state.Total & " / " & state.TargetScore & ". " & If(_session.UndoDescription = "", "История действий пуста.", "Можно отменить: " & _session.UndoDescription)
        _last = state
        _updating = False
    End Sub

    Private Sub CountdownChanged(sender As Object, e As EventArgs)
        _timerStatus.Text = If(_countdown.Running, "Осталось: " & _countdown.SecondsRemaining & " с", If(_countdown.Visible, "Время истекло · 0 с", "Таймер остановлен"))
    End Sub
End Class
