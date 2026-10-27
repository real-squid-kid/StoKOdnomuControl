Option Strict On
Imports System.Windows.Forms

' Два пульта разделяют сессию и таймер. Правки вопроса не заменяются правкой итога.
Public Class BigGameControl
    Inherits Form
    Private ReadOnly _session As GameSession
    Private ReadOnly _countdown As Countdown
    Private ReadOnly _undoAction As Action
    Private ReadOnly _texts(9) As TextBox
    Private ReadOnly _points(9) As NumericUpDown
    Private ReadOnly _textButtons(9) As Button
    Private ReadOnly _pointButtons(9) As Button
    Private ReadOnly _answerTabOrder As New List(Of Control)()
    Private ReadOnly _hide As New CheckBox With {.Name = "MaskPlayer1", .Text = "Скрыть ответы участника 1", .AutoSize = True}
    Private ReadOnly _total As Label = OperatorUi.Label("0")
    Private ReadOnly _target As Label = OperatorUi.Label("/ 150")
    Private ReadOnly _timerValue As Label = OperatorUi.Label("— —")
    Private ReadOnly _menu As New ContextMenuStrip()
    Private _surface As ConsoleLayout
    Private _next As Button
    Private _undo As Button
    Private _last As BigGameState
    Private _updating As Boolean

    Public Sub New(session As GameSession, countdown As Countdown, showBoard As Action, undoAction As Action, Optional hideBoards As Action = Nothing, Optional displaySettings As Action = Nothing, Optional returnToMain As Action = Nothing)
        _session = session
        _countdown = countdown
        _undoAction = undoAction
        BuildUi(showBoard, hideBoards, displaySettings, returnToMain)
        AddHandler _session.Changed, AddressOf SessionChanged
        AddHandler _session.StorageError, AddressOf StorageError
        AddHandler AudioService.Instance.AudioError, AddressOf StorageError
        AddHandler countdown.Changed, AddressOf CountdownChanged
        AddHandler FormClosing, Sub(sender, e)
                                    If e.CloseReason = CloseReason.UserClosing Then
                                        e.Cancel = True
                                        Hide()
                                    End If
                                End Sub
        AddHandler Disposed, Sub()
                                 RemoveHandler _session.Changed, AddressOf SessionChanged
                                 RemoveHandler _session.StorageError, AddressOf StorageError
                                 RemoveHandler AudioService.Instance.AudioError, AddressOf StorageError
                                 RemoveHandler countdown.Changed, AddressOf CountdownChanged
                                 _menu.Dispose()
                             End Sub
        OperatorUi.InstallUndo(Me, _undoAction)
        SessionChanged(Me, EventArgs.Empty)
        CountdownChanged(Me, EventArgs.Empty)
    End Sub

    Private Sub BuildUi(showBoard As Action, hideBoards As Action, displaySettings As Action, returnToMain As Action)
        _surface = New ConsoleLayout(Me, "Сто к одному — большая игра")
        _surface.Toolbar.Controls.Add(OperatorUi.Label("Большая игра", 145))
        OperatorUi.Button(_surface.Toolbar, "Основная игра", Sub()
                                                                    If returnToMain IsNot Nothing Then
                                                                        returnToMain()
                                                                    Else
                                                                        Hide()
                                                                    End If
                                                                End Sub, 160)
        Dim service = OperatorUi.Button(_surface.Toolbar, "Игра…", Sub() Return, 130)
        Dim reset As New ToolStripMenuItem("Новая большая игра…")
        AddHandler reset.Click, Sub() OperatorUi.RunCommand(Me, AddressOf ResetGame)
        _menu.Items.Add(reset)
        AddHandler service.Click, Sub() _menu.Show(service, New Point(0, service.Height))
        Dim reveal = OperatorUi.Group(_surface.Left, "Раскрытие ответов", "BigRevealPanel")
        _next = OperatorUi.Button(reveal, "", AddressOf NextStep)
        _next.Name = "RevealNext"
        BuildPlayersPanel()
        BuildDraftsPanel()
        BuildScorePanel()
        BuildTimerPanel()
        Dim screen = OperatorUi.Group(_surface.Right, "Экран и звук", "BigScreenPanel")
        If displaySettings IsNot Nothing Then OperatorUi.Button(screen, "Настройки…", displaySettings)
        OperatorUi.Button(screen, "Показать табло", showBoard)
        If hideBoards IsNot Nothing Then OperatorUi.Button(screen, "Скрыть оба табло", hideBoards)
        OperatorUi.Button(screen, "Звук повтора ответа", Sub() AudioService.Instance.Play(SoundCue.SameAnswer))
        _undo = OperatorUi.Button(Nothing, "Отменить действие", Sub() _undoAction())
        _undo.Name = "BigUndoLastAction"
        OperatorUi.Cell(_surface.Footer, _undo, 0, 0)
        Dim stopAll = OperatorUi.Button(Nothing, "Остановить все звуки", Sub()
                                                                                    _countdown.Stop()
                                                                                    AudioService.Instance.StopAll()
                                                                                End Sub)
        stopAll.Name = "BigStopAllAudio"
        stopAll.ForeColor = Color.DarkRed
        OperatorUi.Cell(_surface.Footer, stopAll, 2, 0)
    End Sub

    Private Sub BuildPlayersPanel()
        Dim players = OperatorUi.Grid(_surface.Left, -1, -1)
        For player = 0 To 1
            Dim column = OperatorUi.Group(Nothing, "Участник " & (player + 1), "PlayerPanel" & (player + 1))
            OperatorUi.Cell(players, column.Parent, player, 0)
            Dim questions = OperatorUi.Grid(column, 24, 0, 70)
            Dim headings = {"№", "Ответ", "Очки"}
            For c = 0 To 2
                OperatorUi.Cell(questions, OperatorUi.Label(headings(c)), c, 0)
            Next
            For question = 0 To 4
                Dim index = player * 5 + question
                _texts(index) = New TextBox With {.Name = "BigAnswer" & index, .MaxLength = 120, .Margin = New Padding(3)}
                _points(index) = OperatorUi.Number(GameRules.MaximumAnswerPoints)
                _points(index).Name = "BigPoints" & index
                Dim number = OperatorUi.Label((question + 1).ToString())
                number.Height = 24
                OperatorUi.Cell(questions, number, 0, question * 2 + 1)
                OperatorUi.Cell(questions, _texts(index), 1, question * 2 + 1)
                OperatorUi.Cell(questions, _points(index), 2, question * 2 + 1)
                Dim actions = OperatorUi.Grid(Nothing, -1, -1)
                OperatorUi.Cell(questions, actions, 1, question * 2 + 2)
                questions.SetColumnSpan(actions, 2)
                _textButtons(index) = OperatorUi.Button(Nothing, "Показать текст", Sub() ShowText(index))
                _textButtons(index).Name = "ShowBigText" & index
                _pointButtons(index) = OperatorUi.Button(Nothing, "Начислить очки", Sub() ShowPoints(index))
                _pointButtons(index).Name = "ShowBigPoints" & index
                For Each questionButton As Button In New Button() {_textButtons(index), _pointButtons(index)}
                    questionButton.MinimumSize = New Size(0, 30)
                    questionButton.Height = 30
                Next
                OperatorUi.Cell(actions, _textButtons(index), 0, 0)
                OperatorUi.Cell(actions, _pointButtons(index), 1, 0)
            Next
        Next
        ' Сначала ввод всех ответов и очков, затем команды публикации.
        ' Единый порядок пересекает границу двух панелей участников.
        For i = 0 To 9
            _answerTabOrder.Add(_texts(i))
            _answerTabOrder.Add(_points(i))
        Next
        For i = 0 To 9
            _answerTabOrder.Add(_textButtons(i))
            _answerTabOrder.Add(_pointButtons(i))
        Next
    End Sub

    Protected Overrides Function ProcessDialogKey(keyData As Keys) As Boolean
        If (keyData And Keys.KeyCode) = Keys.Tab AndAlso (keyData And (Keys.Control Or Keys.Alt)) = Keys.None Then
            ' NumericUpDown держит фокус во внутреннем TextBox, поэтому проверяем
            ' ContainsFocus. Shift+Tab проходит тот же порядок в обратную сторону.
            Dim current = _answerTabOrder.FindIndex(Function(control) control.ContainsFocus)
            If current >= 0 Then
                Dim direction = If((keyData And Keys.Shift) = Keys.Shift, -1, 1)
                Dim index = current + direction
                While index >= 0 AndAlso index < _answerTabOrder.Count
                    Dim target = _answerTabOrder(index)
                    If target.CanSelect AndAlso target.TabStop Then
                        target.Select()
                        Return True
                    End If
                    index += direction
                End While
            End If
        End If
        ' За пределами группы сохраняем обычную навигацию по остальному пульту.
        Return MyBase.ProcessDialogKey(keyData)
    End Function

    Private Sub BuildDraftsPanel()
        Dim panel = OperatorUi.Group(_surface.Left, "Черновики и видимость", "BigDraftsPanel")
        Dim row = OperatorUi.Row(panel)
        Dim save = OperatorUi.Button(row, "Сохранить черновики", AddressOf SaveDrafts, 220)
        save.Name = "SaveBigDrafts"
        row.Controls.Add(_hide)
        AddHandler _hide.CheckedChanged, Sub()
                                            If Not _updating Then OperatorUi.RunCommand(Me, Sub() _session.Execute("Видимость первого участника", Sub(state) state.Big.HideFirstPlayer = _hide.Checked))
                                        End Sub
        Dim dimFields As New CheckBox With {.Text = "Приглушить поля", .AutoSize = True}
        row.Controls.Add(dimFields)
        AddHandler dimFields.CheckedChanged, Sub()
                                                 For i = 0 To 9
                                                     _texts(i).ForeColor = If(dimFields.Checked, Color.Gray, SystemColors.WindowText)
                                                     _points(i).ForeColor = If(dimFields.Checked, Color.Gray, SystemColors.WindowText)
                                                 Next
                                             End Sub
    End Sub

    Private Sub BuildScorePanel()
        Dim panel = OperatorUi.Group(_surface.Right, "Итог большой игры", "BigScorePanel")
        Dim values = OperatorUi.Grid(panel, -1, -1)
        _total.Name = "BigTotal"
        _target.Name = "BigTarget"
        OperatorUi.LargeValue(_total)
        OperatorUi.Cell(values, _total, 0, 0)
        OperatorUi.Cell(values, _target, 1, 0)
        Dim correct = OperatorUi.Button(panel, "Исправить общий итог…", Sub() OperatorUi.EditNumber(Me, "Итог большой игры", _session.State.Big.Total, GameRules.MaximumScore, Sub(value) Run("Исправить итог большой игры", Sub(state) GameRules.SetBigTotal(state.Big, value))))
        correct.Name = "CorrectBigTotal"
        Dim target = OperatorUi.Button(panel, "Изменить цель…", Sub() OperatorUi.EditNumber(Me, "Цель большой игры", _session.State.Big.TargetScore, GameRules.MaximumScore, Sub(value) Run("Изменить цель", Sub(state) state.Big.TargetScore = value), 1))
        target.Name = "CorrectBigTarget"
    End Sub

    Private Sub BuildTimerPanel()
        Dim panel = OperatorUi.Group(_surface.Right, "Таймер", "BigTimerPanel")
        _timerValue.Name = "CountdownValue"
        _timerValue.TextAlign = ContentAlignment.MiddleCenter
        OperatorUi.LargeValue(_timerValue)
        OperatorUi.Append(panel, _timerValue)
        Dim starts = OperatorUi.Grid(panel, -1, -1)
        Dim twenty = OperatorUi.Button(Nothing, "Начать 20 с", Sub() _countdown.Start(20))
        twenty.Name = "Start20"
        Dim twentyFive = OperatorUi.Button(Nothing, "Начать 25 с", Sub() _countdown.Start(25))
        twentyFive.Name = "Start25"
        OperatorUi.Cell(starts, twenty, 0, 0)
        OperatorUi.Cell(starts, twentyFive, 1, 0)
        Dim stopTimer = OperatorUi.Button(panel, "Остановить таймер и трек", Sub() _countdown.Stop())
        stopTimer.Name = "StopCountdown"
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
            _pointButtons(i).Text = If(answer.ScoreVisible, "Исправить очки", "Начислить очки")
            _pointButtons(i).Enabled = answer.TextVisible
        Next
        _total.Text = state.Total.ToString()
        _target.Text = "/ " & state.TargetScore
        _hide.Checked = state.HideFirstPlayer
        Dim nextStep = state.NextStep
        _next.Enabled = nextStep < 20
        _next.Text = If(nextStep >= 20, "Все ответы и очки открыты", "Далее: " & If(nextStep Mod 2 = 0, "текст", "очки") & " · " & AnswerName(Math.Min(9, nextStep \ 2)))
        _undo.Enabled = _session.UndoDescription <> ""
        _surface.Tips.SetToolTip(_undo, _session.UndoDescription)
        _last = state
        _updating = False
    End Sub

    Private Sub StorageError(message As String)
        OperatorUi.OnUi(Me, Sub() _surface.ShowError(message))
    End Sub

    Private Sub CountdownChanged(sender As Object, e As EventArgs)
        _timerValue.Text = If(_countdown.Visible, _countdown.SecondsRemaining.ToString() & " с", "— —")
    End Sub
End Class
