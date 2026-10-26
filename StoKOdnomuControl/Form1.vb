Option Strict On
Imports System.IO
Imports System.Windows.Forms

' Главный пульт владеет общей сессией, окнами табло и оборудованием.
' Обработчики отправляют команды в GameSession; поля формы не являются счётом игры.
Public Class Form1
    Inherits Form
    Private ReadOnly _session As GameSession
    Private ReadOnly _buzz As New BuzzManager()
    Private _bank As QuestionBank
    ' Предыдущий опубликованный снимок нужен для обновления только изменившихся полей.
    ' Несвязанный рендер не должен стирать ещё не применённый ввод оператора.
    Private _last As MainGameState
    Private _board As Tablo
    Private _bigControl As BigGameControl
    Private _bigBoard As BigGameTablo
    Private ReadOnly _countdown As New Countdown()
    Private ReadOnly _answers(5) As TextBox
    Private ReadOnly _points(5) As NumericUpDown
    Private ReadOnly _show(5) As Button
    Private ReadOnly _answerStatus(5) As Label
    Private ReadOnly _strikes(1, 2) As Button
    Private ReadOnly _scores(2) As NumericUpDown
    Private ReadOnly _roundButtons As New List(Of Button)()
    Private ReadOnly _screens As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Width = 290}
    Private ReadOnly _fullscreen As New CheckBox With {.Text = "На весь экран", .Checked = True, .AutoSize = True, .Margin = New Padding(6, 12, 4, 4)}
    Private ReadOnly _award As New CheckBox With {.Text = "При открытии начислять очки в банк", .Checked = True, .AutoSize = True, .Margin = New Padding(6, 12, 4, 4)}
    Private ReadOnly _round As Label = OperatorUi.Label("", 350)
    Private ReadOnly _status As Label = OperatorUi.Label("Загрузите базу или введите ответы вручную.", 1200)
    Private ReadOnly _buzzStatus As Label = OperatorUi.Label("Buzz!: не подключён", 500)
    Private _undo As Button
    Private _restore As Button

    Public Sub New()
        ' Тесты задают отдельный каталог. В обычном запуске данные принадлежат текущему
        ' пользователю Windows и не записываются в каталог исходников/установки.
        Dim dataDirectory = Environment.GetEnvironmentVariable("STOKODNOMU_DATA_DIR")
        If String.IsNullOrWhiteSpace(dataDirectory) Then dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StoKOdnomuControl")
        _session = New GameSession(dataDirectory)
        BuildUi()
        AddHandler _session.Changed, AddressOf SessionChanged
        AddHandler _session.StorageError, Sub(message) SetStatus(message)
        AddHandler AudioService.Instance.AudioError, AddressOf SetStatus
        AddHandler _countdown.Changed, Sub() RenderCountdown()
        AddHandler _buzz.StatusChanged, Sub(message) OperatorUi.OnUi(Me, Sub() _buzzStatus.Text = message)
        AddHandler _buzz.BuzzerPressed, Sub(pad)
                                           ' Событие приходит с потока HID; контролы и звук запускаем
                                           ' через UI-поток, не блокируя чтение следующего USB-отчёта.
                                           OperatorUi.OnUi(Me, Sub()
                                                                  _buzzStatus.Text = "Первым нажал пульт " & (pad + 1) & ". Для нового розыгрыша нажмите «Разрешить Buzz!» ."
                                                                  AudioService.Instance.Play(SoundCue.Buzzer, AudioChannel.Buzzer)
                                                              End Sub)
                                       End Sub
        AddHandler _buzz.SimultaneousPress, Sub() OperatorUi.OnUi(Me, Sub() _buzzStatus.Text = "Buzz!: несколько нажатий в одном отчёте. Переиграйте розыгрыш.")
        AddHandler Shown, Sub()
                              _buzz.Initialize()
                              _restore.Enabled = _session.HasSavedGame
                          End Sub
        AddHandler FormClosed, AddressOf CloseServices
        KeyPreview = True
        AddHandler KeyDown, Sub(sender, e)
                                If e.Control AndAlso e.KeyCode = Keys.Z Then
                                    UndoAction()
                                    e.SuppressKeyPress = True
                                End If
                            End Sub
        SessionChanged(Me, EventArgs.Empty)
    End Sub

    Private Sub BuildUi()
        ' Пульт собирается программно вместо прежнего Form1.Designer.vb.
        ' Общие размеры/создание контролов — в OperatorUi; здесь остаётся смысл действий.
        Dim page = OperatorUi.Page(Me, "Сто к одному — пульт оператора")
        OperatorUi.Heading(page, "Основная игра")
        Dim actions = OperatorUi.Row(page)
        OperatorUi.Button(actions, "Загрузить базу TXT…", AddressOf LoadBank)
        _undo = OperatorUi.Button(actions, "Отменить действие", AddressOf UndoAction)
        _undo.Name = "UndoLastAction"
        OperatorUi.Button(actions, "Открыть большую игру", AddressOf OpenBigGame)
        _restore = OperatorUi.Button(actions, "Восстановить игру…", AddressOf RestoreGame)
        OperatorUi.Button(actions, "Новый матч…", AddressOf NewMatch, 145)
        OperatorUi.Button(actions, "О программе", Sub()
                                                         Using about As New AboutBox1()
                                                             about.ShowDialog(Me)
                                                         End Using
                                                     End Sub, 140)
        Dim display = OperatorUi.Row(page)
        For i = 0 To Screen.AllScreens.Length - 1
            Dim displayScreen = Screen.AllScreens(i)
            _screens.Items.Add("Экран " & (i + 1) & " · " & displayScreen.Bounds.Width & "×" & displayScreen.Bounds.Height & If(displayScreen.Primary, " · основной", ""))
        Next
        _screens.SelectedIndex = If(Screen.AllScreens.Length > 1, 1, 0)
        display.Controls.Add(_screens)
        display.Controls.Add(_fullscreen)
        OperatorUi.Button(display, "Показать основное табло", AddressOf ShowMainBoard, 235)
        OperatorUi.Button(display, "Скрыть оба табло", AddressOf HideBoards)
        OperatorUi.Button(display, "Остановить все звуки", Sub()
                                                                 _countdown.Stop()
                                                                 AudioService.Instance.StopAll()
                                                             End Sub, 210)
        Dim soundEnabled As New CheckBox With {.Text = "Звук включён", .Checked = True, .AutoSize = True, .Margin = New Padding(6, 12, 4, 4)}
        display.Controls.Add(soundEnabled)
        AddHandler soundEnabled.CheckedChanged, Sub()
                                                    AudioService.Instance.Enabled = soundEnabled.Checked
                                                    If Not soundEnabled.Checked Then AudioService.Instance.StopAll()
                                                End Sub
        Dim rounds = OperatorUi.Row(page)
        For i = 1 To 4
            ' Отдельная локальная копия захватывается обработчиком конкретной кнопки.
            Dim number = i
            _roundButtons.Add(OperatorUi.Button(rounds, {"", "Простая · ×1", "Двойная · ×2", "Тройная · ×3", "Игра наоборот"}(i), Sub() StartRound(number), 138))
        Next
        OperatorUi.Button(rounds, "Сбросить раунд…", AddressOf ResetCurrentRound, 220)
        rounds.Controls.Add(_round)
        Dim scores = OperatorUi.Row(page)
        Dim scoreNames = {"Левая команда", "Банк раунда", "Правая команда"}
        ' Порядок массива полей совпадает с ApplyScore: 0 — слева, 1 — банк, 2 — справа.
        For i = 0 To 2
            Dim index = i
            scores.Controls.Add(OperatorUi.Label(scoreNames(i), 130))
            _scores(i) = OperatorUi.Number()
            _scores(i).Name = {"LeftScore", "RoundBank", "RightScore"}(i)
            scores.Controls.Add(_scores(i))
            Dim apply = OperatorUi.Button(scores, "Применить", Sub() ApplyScore(index), 110)
            apply.Name = "ApplyScore" & index
        Next
        Dim transfers = OperatorUi.Row(page)
        OperatorUi.Button(transfers, "Отдать банк левой команде", Sub() Transfer(TeamSide.Left), 265)
        OperatorUi.Button(transfers, "Отдать банк правой команде", Sub() Transfer(TeamSide.Right), 265)
        transfers.Controls.Add(_award)
        Dim dimFields As New CheckBox With {.Text = "Приглушить поля пульта", .AutoSize = True, .Margin = New Padding(6, 12, 4, 4)}
        transfers.Controls.Add(dimFields)
        AddHandler dimFields.CheckedChanged, Sub()
                                                 For i = 0 To 5
                                                     _answers(i).ForeColor = If(dimFields.Checked, Color.LightGray, SystemColors.WindowText)
                                                     _points(i).ForeColor = If(dimFields.Checked, Color.LightGray, SystemColors.WindowText)
                                                 Next
                                             End Sub
        Dim table = OperatorUi.Grid(page, 42, 0, 95, 170, 180, 190)
        Dim headings = {"№", "Ответ (изменение поля — черновик)", "Очки ×1", "Показ / скрытие", "Правка текста и цены", "Состояние"}
        For column = 0 To headings.Length - 1
            OperatorUi.Cell(table, OperatorUi.Label(headings(column), CInt(If(column = 1, 360, Math.Max(40, table.ColumnStyles(column).Width - 8)))), column, 0)
        Next
        For i = 0 To 5
            Dim index = i
            ' Name — стабильный адрес контрола в проверках кнопок; подпись можно менять.
            _answers(i) = New TextBox With {.Name = "MainAnswer" & i, .MaxLength = 120, .Margin = New Padding(4, 10, 4, 4)}
            _points(i) = OperatorUi.Number(GameRules.MaximumAnswerPoints)
            _points(i).Width = 80
            _points(i).Name = "MainPoints" & i
            OperatorUi.Cell(table, OperatorUi.Label((i + 1).ToString(), 34), 0, i + 1)
            OperatorUi.Cell(table, _answers(i), 1, i + 1)
            OperatorUi.Cell(table, _points(i), 2, i + 1)
            _show(i) = OperatorUi.Button(table, "Открыть ответ", Sub() ToggleAnswer(index), 158)
            _show(i).Name = "ToggleMainAnswer" & i
            table.SetCellPosition(_show(i), New TableLayoutPanelCellPosition(3, i + 1))
            Dim edit = OperatorUi.Button(table, "Применить правку", Sub() EditAnswer(index), 168)
            edit.Name = "EditMainAnswer" & i
            table.SetCellPosition(edit, New TableLayoutPanelCellPosition(4, i + 1))
            _answerStatus(i) = OperatorUi.Label("", 180)
            OperatorUi.Cell(table, _answerStatus(i), 5, i + 1)
        Next
        Dim note = OperatorUi.Label("Скрытие не отменяет очки. Повторный показ не начисляет их снова. Для отмены начисления используйте «Отменить» (Ctrl+Z).", 1200)
        note.Height = 34
        page.Controls.Add(note)
        Dim strikes = OperatorUi.Row(page)
        For side = 0 To 1
            strikes.Controls.Add(OperatorUi.Label(If(side = 0, "Ошибки слева:", "Ошибки справа:"), 145))
            For i = 0 To 2
                Dim team = CType(side, TeamSide)
                Dim index = i
                _strikes(side, i) = OperatorUi.Button(strikes, "Ошибка " & (i + 1), Sub() ToggleStrike(team, index), 130)
            Next
        Next
        Dim splashes = OperatorUi.Row(page)
        splashes.Controls.Add(OperatorUi.Label("Заставка:", 100))
        Dim names = {"Сто к одному", "Простая", "Двойная", "Тройная", "Наоборот", "Большая"}
        For i = 0 To 5
            Dim splash = i
            OperatorUi.Button(splashes, names(i), Sub() _session.Execute("Заставка «" & names(splash) & "»", Sub(state) state.Main.Splash = splash), 140)
        Next
        OperatorUi.Button(splashes, "Вернуть игру", Sub() _session.Execute("Вернуть игру с заставки", Sub(state) state.Main.Splash = -1), 155)
        Dim buzz = OperatorUi.Row(page)
        OperatorUi.Button(buzz, "Подключить Buzz!", Sub() _buzz.Initialize(), 170)
        OperatorUi.Button(buzz, "Разрешить Buzz!", Sub() _buzz.Arm(), 170)
        OperatorUi.Button(buzz, "Заблокировать Buzz!", Sub() _buzz.Disarm(), 200)
        OperatorUi.Button(buzz, "Звук ошибки", Sub() AudioService.Instance.Play(SoundCue.WrongAnswer), 150)
        buzz.Controls.Add(_buzzStatus)
        _buzzStatus.Height = 50
        _status.Height = 42
        _status.ForeColor = Color.FromArgb(25, 64, 110)
        Dim scroll = page.Parent
        ' Нижний статус вынесен из прокрутки, чтобы ошибки сохранения оставались видны.
        Controls.Remove(scroll)
        Dim frame As New TableLayoutPanel With {.Dock = DockStyle.Fill, .RowCount = 2, .ColumnCount = 1, .Margin = New Padding(0), .Padding = New Padding(0)}
        frame.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        frame.RowStyles.Add(New RowStyle(SizeType.Absolute, 42))
        frame.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        frame.Controls.Add(scroll, 0, 0)
        _status.Dock = DockStyle.Fill
        frame.Controls.Add(_status, 0, 1)
        Controls.Add(frame)
    End Sub

    Private Sub LoadBank()
        Using dialog As New OpenFileDialog With {.Title = "Выберите базу из 24 ответов", .Filter = "Базы ответов (*.txt)|*.txt|Все файлы (*.*)|*.*"}
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            Dim loaded = QuestionBankReader.ReadFile(dialog.FileName)
            ' Только полностью проверенная база заменяет предыдущую. До этого момента
            ' отмена диалога/ошибка разбора не меняют ни базу, ни игровую сессию.
            _bank = loaded
            SetStatus("Загружена база «" & Path.GetFileName(dialog.FileName) & "». Выберите раунд. Текущая игра сохранена.")
        End Using
    End Sub

    Private Sub StartRound(number As Integer)
        ' Выбор раунда — одна отменяемая команда: цены остаются базовыми,
        ' множитель, банк, раскрытия и ошибки обновляются согласованно.
        If Not OperatorUi.Confirm(Me, "Начать раунд «" & _roundButtons(number - 1).Text & "»? Банк, открытия и ошибки текущего раунда будут сброшены; счета команд сохранятся.") Then Return
        _last = Nothing
        _session.Execute("Начать раунд " & number, Sub(state)
                                                             state.Main.Round = number
                                                             If _bank IsNot Nothing Then
                                                                 state.Main.Answers = _bank.Rounds(number - 1).Select(Function(a) New AnswerState With {.Text = a.Text, .Points = a.Points}).ToArray()
                                                             ElseIf number = 4 Then
                                                                 Dim values = {15, 30, 45, 60, 120, 240}
                                                                 For i = 0 To 5
                                                                     state.Main.Answers(i).Points = values(i)
                                                                 Next
                                                             End If
                                                             GameRules.ResetRound(state.Main)
                                                         End Sub)
        _buzz.Disarm()
    End Sub

    Private Sub ResetCurrentRound()
        ' Сброс сохраняет счета команд и подготовленные ответы. Новый матч — другая команда.
        If Not OperatorUi.Confirm(Me, "Сбросить банк, открытия и ошибки? Тексты ответов и счета команд сохранятся.") Then Return
        _session.Execute("Сбросить текущий раунд", Sub(state) GameRules.ResetRound(state.Main))
        _buzz.Disarm()
    End Sub

    Private Sub ApplyScore(index As Integer)
        ' В отличие от старых ручных обработчиков стороны здесь не зеркалятся.
        Dim value = CInt(_scores(index).Value)
        _session.Execute("Исправить " & {"счёт левой команды", "банк раунда", "счёт правой команды"}(index) & " → " & value, Sub(state)
                                                                                                                                  Select Case index
                                                                                                                                      Case 0 : state.Main.LeftScore = value
                                                                                                                                      Case 1 : state.Main.Bank = value
                                                                                                                                      Case 2 : state.Main.RightScore = value
                                                                                                                                  End Select
                                                                                                                              End Sub)
    End Sub

    Private Sub Transfer(side As TeamSide)
        _session.Execute("Передать банк " & If(side = TeamSide.Left, "левой", "правой") & " команде", Sub(state) GameRules.TransferBank(state.Main, side))
        ' Эффект воспроизводится после успешной команды. Рендер/Undo его не повторяет.
        AudioService.Instance.Play(SoundCue.Transfer)
    End Sub

    Private Sub ToggleAnswer(index As Integer)
        ' Решение принимается по модели, а не по BackColor. Скрытие убирает только показ;
        ' защитой от двойного начисления управляет GameRules.ShowMainAnswer.
        Dim answer = _session.State.Main.Answers(index)
        If answer.TextVisible Then
            _session.Execute("Скрыть ответ " & (index + 1) & " без отмены очков", Sub(state)
                                                                                     state.Main.Answers(index).TextVisible = False
                                                                                     state.Main.Answers(index).ScoreVisible = False
                                                                                 End Sub)
        Else
            Dim award = _award.Checked
            _session.Execute("Открыть ответ " & (index + 1), Sub(state) GameRules.ShowMainAnswer(state.Main, index, _answers(index).Text, CInt(_points(index).Value), award))
            AudioService.Instance.Play(If(award AndAlso answer.AwardedBank = -1, SoundCue.OpenAnswer, SoundCue.NoScore))
        End If
    End Sub

    Private Sub EditAnswer(index As Integer)
        ' Текст/цена отправляются одной командой; ввод в поле сам не публикует правку.
        _session.Execute("Исправить ответ " & (index + 1), Sub(state) GameRules.EditMainAnswer(state.Main, index, _answers(index).Text, CInt(_points(index).Value)))
    End Sub

    Private Sub ToggleStrike(side As TeamSide, index As Integer)
        Dim wasOn = If(side = TeamSide.Left, _session.State.Main.LeftStrikes(index), _session.State.Main.RightStrikes(index))
        _session.Execute(If(wasOn, "Убрать", "Поставить") & " ошибку " & (index + 1) & If(side = TeamSide.Left, " слева", " справа"), Sub(state)
                                                                                                                                     Dim strikes = If(side = TeamSide.Left, state.Main.LeftStrikes, state.Main.RightStrikes)
                                                                                                                                     strikes(index) = Not wasOn
                                                                                                                                 End Sub)
        If Not wasOn Then AudioService.Instance.Play(SoundCue.WrongAnswer)
    End Sub

    Friend Sub UndoAction()
        ' История общая для обоих пультов. Не сбрасываем _last: тогда нетронутые
        ' черновики других строк переживут отмену одного ответа или начисления.
        _session.Undo()
    End Sub

    Private Sub RestoreGame()
        ' Явное восстановление заменяет и черновики. Runtime-таймер и допуск Buzz!
        ' в сохранение не входят: после загрузки оборудование остаётся остановленным.
        If MessageBox.Show(Me, "Восстановить последнее автосохранение вместе с историей отмены? Текущие несохранённые черновики будут заменены.", "Восстановление", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
        _last = Nothing
        If _bigControl IsNot Nothing Then _bigControl.ReloadDrafts()
        _session.Restore()
        _countdown.Stop()
        _buzz.Disarm()
        SetStatus("Игра восстановлена. Таймер и Buzz! запускаются отдельно. Для следующих раундов при необходимости загрузите базу.")
    End Sub

    Private Sub NewMatch()
        ' Снимок до сброса остаётся в истории, поэтому новый матч тоже можно отменить.
        If Not OperatorUi.Confirm(Me, "Начать новый матч? Все счета, ответы и большая игра будут сброшены.") Then Return
        _last = Nothing
        If _bigControl IsNot Nothing Then _bigControl.ReloadDrafts()
        _session.Execute("Новый матч", Sub(state)
                                          state.Main = New MainGameState()
                                          state.Big = New BigGameState()
                                      End Sub)
        _countdown.Stop()
        _buzz.Disarm()
    End Sub

    Private Sub SessionChanged(sender As Object, e As EventArgs)
        ' Обновляем ввод только при изменении соответствующего значения модели.
        ' Без сравнения с _last каждый клик стирал бы черновики остальных строк.
        Dim state = _session.State.Main
        For i = 0 To 5
            Dim answer = state.Answers(i)
            If _last Is Nothing OrElse _last.Answers(i).Text <> answer.Text Then _answers(i).Text = answer.Text
            If _last Is Nothing OrElse _last.Answers(i).Points <> answer.Points Then _points(i).Value = answer.Points
            _show(i).Text = If(answer.TextVisible, "Скрыть ответ", "Открыть ответ")
            _answerStatus(i).Text = If(answer.AwardedBank >= 0, If(answer.AwardedBank = state.BankId, "+" & answer.AwardedPoints & " в банке", "Очки уже переданы"), "Без начисления")
            _answerStatus(i).ForeColor = If(answer.TextVisible, Color.DarkGreen, Color.DimGray)
        Next
        Dim values = {state.LeftScore, state.Bank, state.RightScore}
        Dim previous = If(_last Is Nothing, Nothing, New Integer() {_last.LeftScore, _last.Bank, _last.RightScore})
        For i = 0 To 2
            If previous Is Nothing OrElse values(i) <> previous(i) Then _scores(i).Value = values(i)
            For side = 0 To 1
                Dim enabled = If(side = 0, state.LeftStrikes(i), state.RightStrikes(i))
                _strikes(side, i).Text = If(enabled, "Убрать ошибку ", "Ошибка ") & (i + 1)
                _strikes(side, i).BackColor = If(enabled, Color.MistyRose, SystemColors.Control)
            Next
        Next
        _round.Text = "Раунд " & state.Round & " · в банк ×" & state.Multiplier & If(_bank Is Nothing, " · ручной ввод", " · база загружена")
        _undo.Enabled = _session.UndoDescription <> ""
        If _undo.Enabled Then _restore.Enabled = True
        _undo.Text = "Отменить (Ctrl+Z)"
        Dim description = _session.UndoDescription
        _status.Text = If(description = "", "Готово. Загрузите базу или введите ответы вручную.", "Последнее действие: " & description & ". Отмена доступна из обоих пультов.")
        If _board IsNot Nothing Then _board.Render(state)
        If _bigBoard IsNot Nothing Then _bigBoard.Render(_session.State.Big)
        _last = state
    End Sub

    Private Function SelectedScreen() As Screen
        ' Состав мониторов мог измениться после запуска; ограничиваем устаревший индекс.
        Dim screens = Screen.AllScreens
        Return screens(Math.Max(0, Math.Min(_screens.SelectedIndex, screens.Length - 1)))
    End Function

    Private Sub ShowMainBoard()
        ' Используем явные экземпляры окон вместо глобальных VB default instances.
        ' На экране показывается один режим; состояние второго продолжает жить в сессии.
        If _bigBoard IsNot Nothing Then _bigBoard.Hide()
        If _board Is Nothing OrElse _board.IsDisposed Then _board = New Tablo()
        _board.Render(_session.State.Main)
        _board.ShowOn(SelectedScreen(), _fullscreen.Checked)
    End Sub

    Private Sub ShowBigBoard()
        If _board IsNot Nothing Then _board.Hide()
        If _bigBoard Is Nothing OrElse _bigBoard.IsDisposed Then _bigBoard = New BigGameTablo()
        _bigBoard.Render(_session.State.Big)
        RenderCountdown()
        _bigBoard.ShowOn(SelectedScreen(), _fullscreen.Checked)
    End Sub

    Private Sub HideBoards()
        If _board IsNot Nothing Then _board.Hide()
        If _bigBoard IsNot Nothing Then _bigBoard.Hide()
    End Sub

    Private Sub OpenBigGame()
        ' Скрытый пульт переиспользуется с черновиками; общие сервисы не создаются повторно.
        _buzz.Disarm()
        If _bigControl Is Nothing OrElse _bigControl.IsDisposed Then _bigControl = New BigGameControl(_session, _countdown, AddressOf ShowBigBoard, AddressOf UndoAction)
        _bigControl.Show(Me)
        _bigControl.BringToFront()
        ShowBigBoard()
    End Sub

    Private Sub RenderCountdown()
        If _bigBoard IsNot Nothing Then _bigBoard.RenderTimer(_countdown.SecondsRemaining, _countdown.Visible)
    End Sub

    Private Sub SetStatus(message As String)
        OperatorUi.OnUi(Me, Sub() _status.Text = message)
    End Sub

    Private Sub CloseServices(sender As Object, e As FormClosedEventArgs)
        ' Единственный владелец закрывает USB, отсчёт и аудиовыход при завершении приложения.
        ' Создание AudioService.Instance не открывает звуковую карту: тихий запуск
        ' завершается без лишней попытки инициализировать аудиоустройство.
        _buzz.Dispose()
        _countdown.Dispose()
        RemoveHandler AudioService.Instance.AudioError, AddressOf SetStatus
        AudioService.Instance.Dispose()
        If _bigControl IsNot Nothing Then _bigControl.Dispose()
        If _board IsNot Nothing Then _board.Dispose()
        If _bigBoard IsNot Nothing Then _bigBoard.Dispose()
    End Sub
End Class
