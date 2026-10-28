Option Strict On
Imports System.IO
Imports System.Windows.Forms

' Главное окно владеет единственной сессией, оборудованием и зрительскими окнами.
Public Class Form1
    Inherits Form
    Private ReadOnly _session As GameSession
    Private ReadOnly _buzz As BuzzManager
    Private ReadOnly _countdown As New Countdown()
    Private _bank As QuestionBank
    Private _last As MainGameState
    Private _board As Tablo
    Private _bigControl As BigGameControl
    Private _bigBoard As BigGameTablo
    Private _surface As ConsoleLayout
    Private ReadOnly _answers(5) As TextBox
    Private ReadOnly _points(5) As NumericUpDown
    Private ReadOnly _show(5) As Button
    Private ReadOnly _awardLabels(5) As Label
    Private ReadOnly _strikes(1, 2) As Button
    Private ReadOnly _scores(2) As Label
    Private ReadOnly _roundButtons As New List(Of Button)()
    Private ReadOnly _award As New CheckBox With {.Text = "Начислять при открытии", .Checked = True, .AutoSize = True}
    Private ReadOnly _bankName As Label = OperatorUi.Label("Ручной ввод", 270)
    Private ReadOnly _buzzStatus As Label = OperatorUi.Label("Не подключён", 270)
    Private ReadOnly _matchMenu As New ContextMenuStrip()
    Private _displaySettings As AppearanceSettings
    Private _bankEditor As BankEditor
    Private ReadOnly _appearance As AppearanceService
    Private _undo As Button
    Private _restore As ToolStripMenuItem
    Private _restoreBackup As ToolStripMenuItem
    Private _connectBuzz As Button
    Private _armBuzz As Button
    Private _blockBuzz As Button
    Private _splashEditor As SplashEditor
    Private _awardHeading As Label
    Private _answerPriceHeading As Label
    Private _closing As Boolean
    Private _confirmingClose As Boolean
    Private _closed As Boolean
    Private _servicesDisposed As Boolean

    Public Sub New()
        Me.New(New BuzzManager())
    End Sub

    ' Подмена только транспорта позволяет проверить настоящий пульт без USB.
    Public Sub New(buzz As BuzzManager)
        _buzz = buzz
        Dim dataDirectory = Environment.GetEnvironmentVariable("STOKODNOMU_DATA_DIR")
        If String.IsNullOrWhiteSpace(dataDirectory) Then dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StoKOdnomuControl")
        dataDirectory = Path.GetFullPath(dataDirectory)
        _session = New GameSession(dataDirectory)
        _appearance = New AppearanceService(Path.Combine(dataDirectory, "appearance"), If(Screen.AllScreens.Length > 1, 1, 0))
        _buzz.LampDelayMilliseconds = _appearance.Current.Profile.BuzzLampDelayMilliseconds
        If File.Exists(AppearanceStorage.ConfigPath(_appearance.Folder)) Then AudioService.Instance.Enabled = _appearance.Current.Profile.SoundEnabled
        BuildUi()
        AddHandler _session.Changed, AddressOf SessionChanged
        AddHandler _session.StorageError, AddressOf SetStatus
        AddHandler AudioService.Instance.AudioError, AddressOf SetStatus
        AddHandler _countdown.Changed, Sub() RenderCountdown()
        AddHandler _buzz.StatusChanged, AddressOf BuzzStatusChanged
        AddHandler _buzz.BuzzerPressed, Sub(pad) OperatorUi.OnUi(Me, Sub()
                                                                      If Not _closing Then AudioService.Instance.Play(SoundCue.Buzzer, AudioChannel.Buzzer)
                                                                  End Sub)
        AddHandler Shown, Async Sub()
                              DisplayScaleWarning.ShowIfNeeded(Me)
                              ResolveStartupChoice()
                              If _closing OrElse IsDisposed Then Return
                              Await _buzz.ConnectAsync()
                              If Not IsDisposed AndAlso Not _closing Then _restore.Enabled = _session.HasSavedGame
                          End Sub
        AddHandler FormClosing, AddressOf CloseAsync
        OperatorUi.InstallUndo(Me, AddressOf UndoAction)
        SessionChanged(Me, EventArgs.Empty)
        RenderBuzz()
        AddHandler _appearance.Changed, AddressOf AppearanceChanged
        ' Конструктор уже выполняется в UI-потоке, но HWND ещё может отсутствовать.
        ' Здесь заполняем сам элемент; OnUi остаётся защитой для фоновых событий.
        If _appearance.StartupError <> "" Then _surface.ShowError(_appearance.StartupError)
    End Sub

    Private Sub BuildUi()
        _surface = New ConsoleLayout(Me, "Сто к одному — основная игра")
        BuildToolbar()
        BuildRoundPanel()
        BuildScoresPanel()
        BuildAnswersPanel()
        BuildSplashPanel()
        BuildBuzzPanel()
        BuildScreenPanel()
        _undo = OperatorUi.Button(Nothing, "Отменить действие", AddressOf UndoAction)
        _undo.Name = "UndoLastAction"
        OperatorUi.Cell(_surface.Footer, _undo, 0, 0)
        Dim stopAll = OperatorUi.Button(Nothing, "Остановить все звуки", AddressOf StopAllAudio)
        stopAll.Name = "StopAllAudio"
        stopAll.ForeColor = Color.DarkRed
        OperatorUi.Cell(_surface.Footer, stopAll, 2, 0)
    End Sub

    Private Sub BuildToolbar()
        _surface.Toolbar.Controls.Add(OperatorUi.Label("Основная игра", 145))
        OperatorUi.Button(_surface.Toolbar, "Большая игра", AddressOf OpenBigGame, 155)
        OperatorUi.Button(_surface.Toolbar, "Загрузить базу…", AddressOf LoadBank, 165)
        Dim editor = OperatorUi.Button(_surface.Toolbar, "Редактор базы…", AddressOf OpenBankEditor, 165)
        editor.Name = "OpenBankEditor"
        Dim service = OperatorUi.Button(_surface.Toolbar, "Матч и сбросы…", Sub() Return, 165)
        _restore = MenuAction("Восстановить игру…", AddressOf RestoreGame)
        _restoreBackup = MenuAction("Восстановить резервную копию…", Sub() RestoreMatch(True))
        _matchMenu.Items.Add(New ToolStripSeparator())
        MenuAction("Сбросить текущий раунд…", AddressOf ResetCurrentRound)
        MenuAction("Новый матч…", AddressOf NewMatch)
        _matchMenu.Items.Add(New ToolStripSeparator())
        MenuAction("О программе…", Sub()
                                        Using about As New AboutBox1()
                                            OperatorWindows.ShowModal(about, Me)
                                        End Using
                                    End Sub)
        AddHandler service.Click, Sub() _matchMenu.Show(service, New Point(0, service.Height))
        _bankName.Name = "LoadedBank"
        _surface.Toolbar.Controls.Add(_bankName)
    End Sub

    Private Function MenuAction(text As String, action As Action) As ToolStripMenuItem
        Dim item As New ToolStripMenuItem(text)
        AddHandler item.Click, Sub() OperatorUi.RunCommand(Me, action)
        _matchMenu.Items.Add(item)
        Return item
    End Function

    Private Sub BuildRoundPanel()
        Dim panel = OperatorUi.Group(_surface.Left, "Раунд", "RoundPanel")
        Dim row = OperatorUi.Grid(panel, -1, -1, -1, -1)
        For i = 1 To 4
            Dim number = i
            Dim button = OperatorUi.Button(Nothing, {"", "Простая · ×1", "Двойная · ×2", "Тройная · ×3", "Игра наоборот"}(i), Sub() StartRound(number))
            button.Name = "StartRound" & i
            _roundButtons.Add(button)
            OperatorUi.Cell(row, button, i - 1, 0)
        Next
    End Sub

    Private Sub BuildScoresPanel()
        Dim panel = OperatorUi.Group(_surface.Left, "Команды и банк", "ScoresPanel")
        Dim columns = OperatorUi.Grid(panel, -1, -1.25F, -1)
        For column = 0 To 2
            Dim index = column
            Dim unit = OperatorUi.Stack()
            unit.Name = "ScoreUnit" & column
            unit.BorderStyle = BorderStyle.FixedSingle
            unit.Padding = New Padding(4)
            unit.Margin = New Padding(3)
            OperatorUi.Cell(columns, unit, column, 0)
            Dim valueRow = OperatorUi.Grid(unit, 0, 105)
            OperatorUi.Cell(valueRow, OperatorUi.Label({"Левая команда", "Банк раунда", "Правая команда"}(column)), 0, 0)
            _scores(column) = OperatorUi.Label("0")
            _scores(column).Name = {"LeftScore", "RoundBank", "RightScore"}(column)
            _scores(column).TextAlign = ContentAlignment.MiddleCenter
            OperatorUi.LargeValue(_scores(column))
            OperatorUi.Cell(valueRow, _scores(column), 1, 0)
            If column = 1 Then
                Dim transfers = OperatorUi.Grid(unit, -1, -1)
                OperatorUi.Cell(transfers, OperatorUi.Button(Nothing, "← Передать левой", Sub() Transfer(TeamSide.Left)), 0, 0)
                OperatorUi.Cell(transfers, OperatorUi.Button(Nothing, "Передать правой →", Sub() Transfer(TeamSide.Right)), 1, 0)
            End If
            Dim correct = OperatorUi.Button(unit, If(column = 1, "Исправить банк…", "Исправить счёт…"), Sub() OperatorUi.EditNumber(Me, {"Счёт левой команды", "Банк раунда", "Счёт правой команды"}(index), Integer.Parse(_scores(index).Text), GameRules.MaximumScore, Sub(value) ApplyScore(index, value)))
            correct.Name = "CorrectScore" & column
            If column <> 1 Then
                Dim side = If(column = 0, TeamSide.Left, TeamSide.Right)
                Dim strikes = OperatorUi.Grid(unit, -1, -1, -1)
                For i = 0 To 2
                    Dim strike = i
                    _strikes(CInt(side), i) = OperatorUi.Button(Nothing, "× " & (i + 1), Sub() ToggleStrike(side, strike))
                    _strikes(CInt(side), i).Name = If(side = TeamSide.Left, "LeftStrike", "RightStrike") & i
                    _strikes(CInt(side), i).AccessibleName = "Промах " & (i + 1) & If(side = TeamSide.Left, " левой команды", " правой команды")
                    OperatorUi.Cell(strikes, _strikes(CInt(side), i), i, 0)
                Next
            End If
        Next
    End Sub

    Private Sub BuildAnswersPanel()
        Dim panel = OperatorUi.Group(_surface.Left, "Ответы", "AnswersPanel")
        Dim options = OperatorUi.Row(panel)
        options.Controls.Add(_award)
        Dim dimFields As New CheckBox With {.Text = "Приглушить поля", .AutoSize = True, .Margin = New Padding(12, 0, 0, 0)}
        options.Controls.Add(dimFields)
        AddHandler dimFields.CheckedChanged, Sub()
                                                 For i = 0 To 5
                                                     _answers(i).ForeColor = If(dimFields.Checked, Color.Gray, SystemColors.WindowText)
                                                     _points(i).ForeColor = If(dimFields.Checked, Color.Gray, SystemColors.WindowText)
                                                 Next
                                             End Sub
        Dim table = OperatorUi.Grid(panel, 28, 0, 75, 65, 137, 137)
        _answerPriceHeading = OperatorUi.Label("Ответили" & Environment.NewLine & "чел.")
        _awardHeading = OperatorUi.Label("В банк" & Environment.NewLine & "×1")
        For Each heading As Label In New Label() {_answerPriceHeading, _awardHeading}
            heading.Height = 42
            heading.AutoEllipsis = False
        Next
        Dim headings = {OperatorUi.Label("№"), OperatorUi.Label("Ответ"), _answerPriceHeading, _awardHeading, OperatorUi.Label("Показ"), OperatorUi.Label("Правка")}
        For c = 0 To headings.Length - 1
            OperatorUi.Cell(table, headings(c), c, 0)
        Next
        For i = 0 To 5
            Dim index = i
            _answers(i) = New TextBox With {.Name = "MainAnswer" & i, .MaxLength = 120, .Margin = New Padding(3)}
            _points(i) = OperatorUi.Number(GameRules.MaximumAnswerPoints)
            _points(i).Name = "MainPoints" & i
            AddHandler _points(i).ValueChanged, Sub() RefreshAward(index)
            _awardLabels(i) = OperatorUi.Label("0")
            _awardLabels(i).Name = "MainAward" & i
            _show(i) = OperatorUi.Button(Nothing, "Открыть ответ", Sub() ToggleAnswer(index))
            _show(i).Name = "ToggleMainAnswer" & i
            Dim edit = OperatorUi.Button(Nothing, "Исправить ответ", Sub() EditAnswer(index))
            edit.Name = "EditMainAnswer" & i
            For Each answerButton As Button In New Button() {_show(i), edit}
                answerButton.MinimumSize = New Size(0, 30)
                answerButton.Height = 32
            Next
            Dim controls As Control() = {OperatorUi.Label((i + 1).ToString()), _answers(i), _points(i), _awardLabels(i), _show(i), edit}
            For c = 0 To controls.Length - 1
                OperatorUi.Cell(table, controls(c), c, i + 1)
            Next
        Next
    End Sub

    Private Sub RefreshAward(index As Integer)
        Dim multiplier = If(_last Is Nothing, 1, _last.Multiplier)
        _awardLabels(index).Text = (CInt(_points(index).Value) * multiplier).ToString()
    End Sub

    Private Sub BuildSplashPanel()
        Dim panel = OperatorUi.Group(_surface.Right, "Заставка", "SplashPanel")
        _splashEditor = New SplashEditor(Sub(line1, line2) _session.Execute("Показать заставку", Sub(state) GameRules.ShowSplash(state.Main.SplashScreen, line1, line2)),
                                         Sub() _session.Execute("Вернуть ответы", Sub(state) state.Main.SplashScreen.Visible = False),
                                         AddressOf PreviewSplash)
        OperatorUi.Append(panel, _splashEditor)
    End Sub

    Private Sub PreviewSplash(line1 As String, line2 As String)
        Dim state = _session.State.Main
        GameRules.ShowSplash(state.SplashScreen, line1, line2)
        ' Предпросмотр получает копию состояния и своё окно; сессию и живое табло не меняет.
        Using preview As New Tablo(True)
            preview.Text = "Предпросмотр заставки"
            preview.Name = "SplashPreview"
            preview.ShowInTaskbar = False
            preview.ClientSize = New Size(900, 485)
            preview.ApplyAppearance(_appearance.Current, state.Round)
            preview.Render(state)
            OperatorWindows.ShowModal(preview, Me)
        End Using
    End Sub

    Private Sub BuildBuzzPanel()
        Dim panel = OperatorUi.Group(_surface.Right, "Buzz!", "BuzzPanel")
        _buzzStatus.Name = "BuzzStatus"
        OperatorUi.Append(panel, _buzzStatus)
        _connectBuzz = OperatorUi.AsyncButton(panel, "Подключить Buzz!", Function() _buzz.ConnectAsync())
        _connectBuzz.Name = "ConnectBuzz"
        Dim commands = OperatorUi.Grid(panel, -1, -1)
        _armBuzz = OperatorUi.Button(Nothing, "Разрешить приём", Sub() _buzz.Arm())
        _armBuzz.Name = "ArmBuzz"
        _blockBuzz = OperatorUi.Button(Nothing, "Заблокировать", Sub() _buzz.Disarm())
        _blockBuzz.Name = "BlockBuzz"
        OperatorUi.Cell(commands, _armBuzz, 0, 0)
        OperatorUi.Cell(commands, _blockBuzz, 1, 0)
    End Sub

    Private Sub BuzzStatusChanged(sender As Object, e As EventArgs)
        OperatorUi.OnUi(Me, AddressOf RenderBuzz)
    End Sub

    Private Sub RenderBuzz()
        Dim status = _buzz.Status
        _connectBuzz.Enabled = status.CanConnect
        _armBuzz.Enabled = status.CanArm
        _blockBuzz.Enabled = status.CanDisarm
        _buzzStatus.Text = status.Message & If(status.Warning = "", "", Environment.NewLine & status.Warning)
        _buzzStatus.Height = If(status.Warning = "", 32, 56)
        _buzzStatus.ForeColor = If(status.State = BuzzState.Faulted OrElse status.Warning <> "", Color.DarkRed, SystemColors.ControlText)
        _surface.Tips.SetToolTip(_buzzStatus, _buzzStatus.Text)
    End Sub

    Private Sub BuildScreenPanel()
        Dim panel = OperatorUi.Group(_surface.Right, "Экран и звук", "ScreenPanel")
        Dim settings = OperatorUi.Button(panel, "Настройки…", AddressOf ShowDisplaySettings)
        settings.Name = "OpenSettings"
        OperatorUi.Button(panel, "Показать табло", AddressOf ShowMainBoard)
        OperatorUi.Button(panel, "Скрыть оба табло", AddressOf HideBoards)
        OperatorUi.Button(panel, "Звук промаха", Sub() AudioService.Instance.Play(SoundCue.WrongAnswer))
    End Sub

    Private Sub ShowDisplaySettings()
        If _displaySettings Is Nothing OrElse _displaySettings.IsDisposed Then _displaySettings = New AppearanceSettings(_appearance)
        OperatorWindows.CenterOnPrimary(_displaySettings)
        If Not _displaySettings.Visible Then _displaySettings.Show(Me)
        _displaySettings.BringToFront()
    End Sub

    Private Sub OpenBankEditor()
        If _bankEditor Is Nothing OrElse _bankEditor.IsDisposed Then _bankEditor = New BankEditor(AddressOf LoadBankFile)
        OperatorWindows.CenterOnPrimary(_bankEditor)
        If Not _bankEditor.Visible Then _bankEditor.Show(Me)
        _bankEditor.BringToFront()
    End Sub

    Private Sub AppearanceChanged(sender As Object, e As EventArgs)
        Dim profile = _appearance.Current.Profile
        _buzz.LampDelayMilliseconds = profile.BuzzLampDelayMilliseconds
        AudioService.Instance.Enabled = profile.SoundEnabled
        If Not profile.SoundEnabled Then AudioService.Instance.StopAll()
        If _board IsNot Nothing Then
            _board.ApplyAppearance(_appearance.Current, _session.State.Main.Round)
            _board.Render(_session.State.Main)
            If _board.Visible Then _board.ShowOn(SelectedScreen(), profile.FullScreen)
        End If
        If _bigBoard IsNot Nothing Then
            _bigBoard.ApplyAppearance(_appearance.Current)
            _bigBoard.Render(_session.State.Big)
            If _bigBoard.Visible Then _bigBoard.ShowOn(SelectedScreen(), profile.FullScreen)
        End If
        RenderCountdown()
    End Sub

    Private Sub StopAllAudio()
        _countdown.Stop()
        AudioService.Instance.StopAll()
    End Sub

    Private Sub LoadBank()
        Using dialog As New OpenFileDialog With {.Title = "Выберите базу из 24 ответов", .InitialDirectory = ApplicationFiles.BanksDirectory, .Filter = "Базы ответов (*.txt)|*.txt|Все файлы (*.*)|*.*"}
            If OperatorWindows.ShowFileDialog(dialog, Me) <> DialogResult.OK Then Return
            LoadBankFile(dialog.FileName)
        End Using
    End Sub

    Private Sub LoadBankFile(path As String)
        Dim loaded = QuestionBankReader.ReadFile(path)
        ' Ни сохранение редактора, ни загрузка новой базы не меняют текущий матч.
        _bank = loaded
        _bankName.Text = IO.Path.GetFileName(path)
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
                                                                 Dim values = RoundRules.ReversePrices()
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

    Private Sub ApplyScore(index As Integer, value As Integer)
        ' В отличие от старых ручных обработчиков стороны здесь не зеркалятся.
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
        RestoreMatch(False)
    End Sub

    Private Sub RestoreMatch(useBackup As Boolean)
        Dim source = If(useBackup, "предыдущую резервную копию", "последнее автосохранение")
        If OperatorWindows.Message(Me, "Восстановить " & source & " вместе с историей отмены? Текущие несохранённые черновики будут заменены.", "Восстановление", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then Return
        ' Ошибка чтения не должна сбрасывать даже несохранённые поля пульта.
        ' Сначала сервис проверяет файл целиком; только успех заменяет черновики.
        _session.Restore(useBackup)
        ReloadMatchDrafts()
    End Sub

    Private Sub NewMatch()
        If Not OperatorUi.Confirm(Me, "Начать новый матч? Прежние сохранения будут помещены в отдельный архив; все счета и ответы будут сброшены.") Then Return
        _session.StartNewMatch()
        ReloadMatchDrafts()
    End Sub

    Private Sub ReloadMatchDrafts()
        _last = Nothing
        _splashEditor.ReloadDrafts()
        If _bigControl IsNot Nothing Then _bigControl.ReloadDrafts()
        SessionChanged(Me, EventArgs.Empty)
        _countdown.Stop()
        _buzz.Disarm()
    End Sub

    Private Function PendingStartupMessage() As String
        Return String.Join(Environment.NewLine, New String() {_appearance.StartupError, "Найден сохранённый матч. Восстановите его или выберите «Новый матч…» в меню «Матч и сбросы…»."}.Where(Function(line) line <> ""))
    End Function

    Private Function ResolveStartupChoice() As Boolean
        If Not _session.RequiresStartupChoice Then Return True
        Using dialog As New Form With {.Name = "StartupMatchChoice", .Text = "Найден сохранённый матч", .Font = Font, .ClientSize = New Size(680, 310), .FormBorderStyle = FormBorderStyle.FixedDialog, .MaximizeBox = False, .MinimizeBox = False, .ShowInTaskbar = False}
            Dim body = OperatorUi.Stack()
            body.Dock = DockStyle.Fill
            body.Padding = New Padding(14)
            OperatorUi.Append(body, New DialogHeading("Продолжить матч или начать новый?"))
            Dim note = OperatorUi.Label("Новый матч сохранит прежние файлы отдельно от вращающегося резерва.")
            note.Height = 38
            OperatorUi.Append(body, note)
            Dim files As New ComboBox With {.Name = "StartupSaveSource", .DropDownStyle = ComboBoxStyle.DropDownList}
            Dim backups As New List(Of Boolean)()
            If _session.HasSavedGame Then
                files.Items.Add("Последнее автосохранение")
                backups.Add(False)
            End If
            If _session.HasSavedBackup Then
                files.Items.Add("Предыдущая резервная копия")
                backups.Add(True)
            End If
            If backups.Count = 0 Then Return True
            files.SelectedIndex = 0
            OperatorUi.Append(body, files)
            Dim errorLabel = OperatorUi.Label("")
            errorLabel.Name = "StartupMatchError"
            errorLabel.ForeColor = Color.DarkRed
            errorLabel.AutoSize = True
            errorLabel.MaximumSize = New Size(640, 0)
            OperatorUi.Append(body, errorLabel)
            Dim actions = OperatorUi.Grid(body, -1, -1, -1)
            Dim restore = OperatorUi.Button(Nothing, "Восстановить", Sub()
                                                                         Try
                                                                             _session.Restore(backups(files.SelectedIndex))
                                                                             ReloadMatchDrafts()
                                                                             dialog.DialogResult = DialogResult.OK
                                                                         Catch ex As Exception
                                                                             errorLabel.Text = ex.Message
                                                                         End Try
                                                                     End Sub)
            restore.Name = "StartupRestore"
            Dim fresh = OperatorUi.Button(Nothing, "Начать новый матч", Sub()
                                                                               Try
                                                                                   _session.StartNewMatch()
                                                                                   ReloadMatchDrafts()
                                                                                   dialog.DialogResult = DialogResult.OK
                                                                               Catch ex As Exception
                                                                                   errorLabel.Text = ex.Message
                                                                               End Try
                                                                           End Sub)
            fresh.Name = "StartupNewMatch"
            Dim cancel = OperatorUi.Button(Nothing, "Отмена", Sub() dialog.DialogResult = DialogResult.Cancel)
            cancel.Name = "StartupCancel"
            OperatorUi.Cell(actions, restore, 0, 0)
            OperatorUi.Cell(actions, fresh, 1, 0)
            OperatorUi.Cell(actions, cancel, 2, 0)
            dialog.Controls.Add(body)
            dialog.AcceptButton = cancel
            dialog.CancelButton = cancel
            dialog.ActiveControl = cancel
            OperatorWindows.ShowModal(dialog, Me)
        End Using
        If _session.RequiresStartupChoice Then _surface.ShowError(PendingStartupMessage())
        Return Not _session.RequiresStartupChoice
    End Function

    Private Sub SessionChanged(sender As Object, e As EventArgs)
        ' Не трогаем несвязанные черновики и не заменяем сообщение об ошибке
        ' очередным успешным действием. Обычного журнала событий у пульта нет.
        Dim state = _session.State.Main
        Dim ready = Not _session.RequiresStartupChoice
        For Each panelName As String In {"RoundPanel", "ScoresPanel", "AnswersPanel", "SplashPanel"}
            For Each gameGroup As Control In Controls.Find(panelName, True)
                gameGroup.Enabled = ready
            Next
        Next
        If ready AndAlso _surface.ErrorLabel.Text = PendingStartupMessage() Then _surface.ShowError(_appearance.StartupError)
        For i = 0 To 5
            Dim answer = state.Answers(i)
            If _last Is Nothing OrElse _last.Answers(i).Text <> answer.Text Then _answers(i).Text = answer.Text
            If _last Is Nothing OrElse _last.Answers(i).Points <> answer.Points Then _points(i).Value = answer.Points
            _show(i).Text = If(answer.TextVisible, "Скрыть ответ", "Открыть ответ")
        Next
        Dim values = {state.LeftScore, state.Bank, state.RightScore}
        For i = 0 To 2
            _scores(i).Text = values(i).ToString()
            For side = 0 To 1
                Dim active = If(side = 0, state.LeftStrikes(i), state.RightStrikes(i))
                _strikes(side, i).Text = "× " & (i + 1)
                OperatorUi.Selected(_strikes(side, i), active)
                _strikes(side, i).BackColor = If(active, Color.MistyRose, SystemColors.Control)
                _strikes(side, i).ForeColor = If(active, Color.DarkRed, SystemColors.ControlText)
                _strikes(side, i).AccessibleDescription = If(active, "Промах поставлен", "Промаха нет")
                _surface.Tips.SetToolTip(_strikes(side, i), If(active, "Убрать промах ", "Поставить промах ") & (i + 1))
            Next
        Next
        For i = 0 To _roundButtons.Count - 1
            OperatorUi.Selected(_roundButtons(i), state.Round = i + 1)
        Next
        _answerPriceHeading.Text = If(state.Round = 4, "Цена", "Ответили" & Environment.NewLine & "чел.")
        _awardHeading.Text = "В банк" & Environment.NewLine & "×" & state.Multiplier
        _undo.Enabled = _session.UndoDescription <> ""
        _surface.Tips.SetToolTip(_undo, _session.UndoDescription)
        _restore.Enabled = _session.HasSavedGame
        _restoreBackup.Enabled = _session.HasSavedBackup
        _splashEditor.Render(state.SplashScreen)
        If _board IsNot Nothing Then _board.Render(state)
        If _bigBoard IsNot Nothing Then _bigBoard.Render(_session.State.Big)
        _last = state
        For i = 0 To 5
            RefreshAward(i)
        Next
    End Sub

    Private Function SelectedScreen() As Screen
        ' Номер из профиля ограничивается фактическим составом мониторов.
        Dim screens = Screen.AllScreens
        Return screens(Math.Min(_appearance.Current.Profile.ScreenIndex, screens.Length - 1))
    End Function

    Private Sub ShowMainBoard()
        If Not ResolveStartupChoice() Then Return
        ' Используем явные экземпляры окон вместо глобальных VB default instances.
        ' На экране показывается один режим; состояние второго продолжает жить в сессии.
        If _bigBoard IsNot Nothing Then _bigBoard.Hide()
        If _board Is Nothing OrElse _board.IsDisposed Then _board = New Tablo()
        _board.ApplyAppearance(_appearance.Current, _session.State.Main.Round)
        _board.Render(_session.State.Main)
        _board.ShowOn(SelectedScreen(), _appearance.Current.Profile.FullScreen)
    End Sub

    Private Sub ShowBigBoard()
        If Not ResolveStartupChoice() Then Return
        If _board IsNot Nothing Then _board.Hide()
        If _bigBoard Is Nothing OrElse _bigBoard.IsDisposed Then _bigBoard = New BigGameTablo()
        _bigBoard.ApplyAppearance(_appearance.Current)
        _bigBoard.Render(_session.State.Big)
        RenderCountdown()
        _bigBoard.ShowOn(SelectedScreen(), _appearance.Current.Profile.FullScreen)
    End Sub

    Private Sub HideBoards()
        If _board IsNot Nothing Then _board.Hide()
        If _bigBoard IsNot Nothing Then _bigBoard.Hide()
    End Sub

    Private Sub OpenBigGame()
        If Not ResolveStartupChoice() Then Return
        ' Скрытый пульт переиспользуется с черновиками; общие сервисы не создаются повторно.
        _buzz.Disarm()
        If _bigControl Is Nothing OrElse _bigControl.IsDisposed Then _bigControl = New BigGameControl(_session, _countdown, AddressOf ShowBigBoard, AddressOf UndoAction, AddressOf HideBoards, AddressOf ShowDisplaySettings, AddressOf ReturnToMain)
        OperatorWindows.CenterOnPrimary(_bigControl)
        _bigControl.Show(Me)
        _bigControl.BringToFront()
        ShowBigBoard()
    End Sub

    Private Sub RenderCountdown()
        If _bigBoard IsNot Nothing Then _bigBoard.RenderTimer(_countdown.SecondsRemaining, _countdown.Visible)
    End Sub

    Private Sub ReturnToMain()
        If _bigControl IsNot Nothing Then _bigControl.Hide()
        OperatorWindows.CenterOnPrimary(Me)
        Activate()
        ShowMainBoard()
    End Sub

    Private Sub SetStatus(message As String)
        OperatorUi.OnUi(Me, Sub() _surface.ShowError(message))
    End Sub

    Private Async Sub CloseAsync(sender As Object, e As FormClosingEventArgs)
        If _closed Then Return
        e.Cancel = True
        If _closing OrElse _confirmingClose Then Return
        If e.CloseReason = CloseReason.UserClosing Then
            _confirmingClose = True
            Try
                If Not OperatorUi.ConfirmExit(Me) Then Return
            Finally
                _confirmingClose = False
            End Try
        End If
        ' До ответа «Да» ничего не останавливаем. Повторный Close после await
        ' пропускается по _closed и не открывает второе подтверждение.
        If _displaySettings IsNot Nothing AndAlso Not _displaySettings.IsDisposed AndAlso Not _displaySettings.ConfirmPending() Then Return
        If _bankEditor IsNot Nothing AndAlso Not _bankEditor.IsDisposed AndAlso Not _bankEditor.ConfirmPending() Then Return
        _closing = True
        Enabled = False
        If _bigControl IsNot Nothing Then _bigControl.Enabled = False
        If _displaySettings IsNot Nothing Then _displaySettings.Enabled = False
        If _bankEditor IsNot Nothing Then _bankEditor.Enabled = False
        StopAllAudio()
        HideBoards()
        ' Продолжаем обрабатывать Windows-сообщения, пока worker освобождает USB.
        Try
            Await _buzz.ShutdownAsync()
        Catch ex As Exception
            Diagnostics.Debug.WriteLine(ex.Message)
        End Try
        _closed = True
        ' Даже без приёмника ShutdownAsync может завершиться синхронно.
        ' Следующий WM_CLOSE отправляем после выхода из текущего FormClosing.
        If Not IsDisposed AndAlso IsHandleCreated Then BeginInvoke(New Action(AddressOf Close))
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing AndAlso Not _servicesDisposed Then
            _servicesDisposed = True
            If _buzz IsNot Nothing Then
                RemoveHandler _buzz.StatusChanged, AddressOf BuzzStatusChanged
            End If
            RemoveHandler AudioService.Instance.AudioError, AddressOf SetStatus
            ' Ошибка одного драйвера при выходе не должна оставлять остальные
            ' сервисы и окна неосвобождёнными. Аудиовыход закрываем последним.
            For Each resource As IDisposable In New IDisposable() {_buzz, _countdown, _bigControl, _displaySettings, _bankEditor, _board, _bigBoard, _appearance, _matchMenu, AudioService.Instance}
                If resource Is Nothing Then Continue For
                Try
                    resource.Dispose()
                Catch ex As Exception
                    Diagnostics.Debug.WriteLine(ex.Message)
                End Try
            Next
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
