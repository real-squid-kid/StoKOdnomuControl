Option Strict On
Imports System.IO
Imports System.Windows.Forms

Public NotInheritable Class AppearanceSettings
    Inherits Form
    Private ReadOnly _service As AppearanceService
    Private _draft As AppearanceProfile
    Private ReadOnly _name As New TextBox With {.Name = "ProfileName", .MaxLength = 80}
    Private ReadOnly _scope As New ComboBox With {.Name = "AppearanceScope", .DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly _inherit As New CheckBox With {.Name = "InheritAppearance", .Text = "Общее оформление основного табло", .AutoSize = True}
    Private ReadOnly _paths(4) As TextBox
    Private ReadOnly _assetButtons As New List(Of Button)()
    Private ReadOnly _color As Button
    Private ReadOnly _screens As New ComboBox With {.Name = "SettingsScreen", .DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly _fullscreen As New CheckBox With {.Name = "SettingsFullscreen", .Text = "На весь экран", .AutoSize = True}
    Private ReadOnly _sound As New CheckBox With {.Name = "SettingsSound", .Text = "Звук включён", .AutoSize = True}
    Private ReadOnly _buzzDelay As New NumericUpDown With {.Name = "SettingsBuzzLampDelay", .Minimum = 0, .Maximum = BuzzManager.MaxLampDelayMilliseconds, .Increment = 25, .Width = 120}
    Private ReadOnly _error As Label = OperatorUi.Label("")
    Private ReadOnly _profiles As New ComboBox With {.Name = "ProfileList", .DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly _apply As Button
    Private _selectedProfileIndex As Integer
    Private _selectingProfile As Boolean
    Private _filling As Boolean
    Public ReadOnly Property Dirty As Boolean

    Public Sub New(service As AppearanceService)
        _service = service
        _draft = service.Current.Profile
        Name = "AppearanceSettings"
        Text = "Настройки"
        Font = New Font("Segoe UI", 10)
        ClientSize = New Size(1080, 710)
        MinimumSize = New Size(980, 690)
        OperatorWindows.Install(Me)
        Dim frame As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 4, .Padding = New Padding(12)}
        frame.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        frame.RowStyles.Add(New RowStyle(SizeType.Absolute, 52))
        frame.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        frame.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        frame.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
        Dim tabs As New TabControl With {.Dock = DockStyle.Fill, .Name = "SettingsTabs"}
        Dim appearance As New TabPage("Оформление") With {.Padding = New Padding(10), .AutoScroll = True}
        Dim body = OperatorUi.Stack()
        body.Dock = DockStyle.Top
        Dim profileRow = OperatorUi.Grid(body, 130, 0, 165, 210)
        OperatorUi.Cell(profileRow, OperatorUi.Label("Профиль"), 0, 0)
        OperatorUi.Cell(profileRow, _profiles, 1, 0)
        OperatorUi.Cell(profileRow, Command("Открыть профиль…", "OpenProfile", AddressOf OpenProfileDialog), 2, 0)
        OperatorUi.Cell(profileRow, Command("Сохранить в папку…", "ExportProfile", AddressOf ExportProfile), 3, 0)
        Dim nameRow = OperatorUi.Grid(body, 130, 0)
        OperatorUi.Cell(nameRow, OperatorUi.Label("Название"), 0, 0)
        OperatorUi.Cell(nameRow, _name, 1, 0)
        Dim scopeRow = OperatorUi.Grid(body, 130, 270, 0)
        OperatorUi.Cell(scopeRow, OperatorUi.Label("Настраиваем"), 0, 0)
        _scope.Items.AddRange(New Object() {"Основное табло — общее", "Простая игра", "Двойная игра", "Тройная игра", "Игра наоборот", "Большая игра"})
        OperatorUi.Cell(scopeRow, _scope, 1, 0)
        OperatorUi.Cell(scopeRow, _inherit, 2, 0)
        Dim assetGrid = OperatorUi.Grid(body, 190, 0, 120, 125)
        For i = 0 To 4
            Dim index = i
            _paths(i) = New TextBox With {.Name = "AppearanceAsset" & i, .ReadOnly = True, .Margin = New Padding(3, 7, 3, 7)}
            OperatorUi.Cell(assetGrid, OperatorUi.Label(New String() {"Подложка табло", "Слева — промаха нет", "Слева — промах есть", "Справа — промаха нет", "Справа — промах есть"}(i), 190), 0, i)
            OperatorUi.Cell(assetGrid, _paths(i), 1, i)
            Dim choose = Command("Выбрать…", "ChooseAsset" & i, Sub() ChooseAsset(index))
            Dim reset = Command("Сброс", "ResetAsset" & i, Sub() SetAsset(index, ""))
            _assetButtons.Add(choose)
            _assetButtons.Add(reset)
            OperatorUi.Cell(assetGrid, choose, 2, i)
            OperatorUi.Cell(assetGrid, reset, 3, i)
        Next
        Dim colors = OperatorUi.Grid(body, 190, 0, 190)
        OperatorUi.Cell(colors, OperatorUi.Label("Цвет текста"), 0, 0)
        _color = Command("Выбрать цвет…", "AppearanceColor", AddressOf ChooseColor)
        OperatorUi.Cell(colors, _color, 1, 0)
        OperatorUi.Cell(colors, Command("Стандартный цвет", "ResetTextColor", Sub()
                                                                                   Style().TextArgb = New BoardStyle().TextArgb
                                                                                   MarkDirty()
                                                                                   FillStyle()
                                                                               End Sub), 2, 0)
        Dim previews As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True}
        previews.Controls.Add(Command("Превью табло", "PreviewAppearanceMain", Sub() Preview(False, False), 170))
        previews.Controls.Add(Command("Превью заставки", "PreviewAppearanceSplash", Sub() Preview(False, True), 185))
        previews.Controls.Add(Command("Превью большой игры", "PreviewAppearanceBig", Sub() Preview(True, False), 230))
        previews.Controls.Add(Command("Сброс оформления…", "ResetAppearance", Sub()
                                                                                     If OperatorWindows.Message(Me, "Вернуть оформление «По умолчанию» для всех раундов? Имя профиля, экран и звук сохранятся.", "Оформление", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
                                                                                     _draft.Main = New BoardStyle()
                                                                                     _draft.Rounds = New AppearanceProfile().Rounds
                                                                                     _draft.Big = New BoardStyle()
                                                                                     MarkDirty()
                                                                                     FillStyle()
                                                                                 End Sub, 220))
        OperatorUi.Append(body, previews)
        Dim note = OperatorUi.Label("PNG / JPEG · холст 1264×681. Новая подложка использует существующие места ответов и счёта. Для переноса профиля копируйте всю сохранённую папку.")
        note.Height = 52
        OperatorUi.Append(body, note)
        appearance.Controls.Add(body)
        tabs.TabPages.Add(appearance)
        Dim display As New TabPage("Экран и звук") With {.Padding = New Padding(20)}
        Dim fields = OperatorUi.Stack()
        fields.Dock = DockStyle.Top
        For i = 0 To Screen.AllScreens.Length - 1
            Dim displayScreen = Screen.AllScreens(i)
            _screens.Items.Add("Экран " & (i + 1) & " · " & displayScreen.Bounds.Width & "×" & displayScreen.Bounds.Height)
        Next
        OperatorUi.Append(fields, OperatorUi.Label("Зрительский экран", 260))
        OperatorUi.Append(fields, _screens)
        OperatorUi.Append(fields, _fullscreen)
        OperatorUi.Append(fields, _sound)
        Dim buzzTiming = OperatorUi.Grid(fields, 340, 120, 0)
        OperatorUi.Cell(buzzTiming, OperatorUi.Label("Задержка мигания кнопки Buzz!, мс", 340), 0, 0)
        OperatorUi.Cell(buzzTiming, _buzzDelay, 1, 0)
        OperatorUi.Cell(buzzTiming, OperatorUi.Label("0 — без задержки"), 2, 0)
        display.Controls.Add(fields)
        tabs.TabPages.Add(display)
        frame.Controls.Add(New DialogHeading("Настройки"), 0, 0)
        frame.Controls.Add(tabs, 0, 1)
        _error.Name = "SettingsError"
        _error.ForeColor = Color.DarkRed
        _error.AutoSize = True
        _error.MaximumSize = New Size(990, 0)
        frame.Controls.Add(_error, 0, 2)
        Dim cancel = Command("Отмена", "CancelSettings", AddressOf Close, 130)
        _apply = Command("Применить", "ApplySettings", AddressOf ApplyDraft, 150)
        Dim ok = Command("ОК", "OkSettings", Sub()
                                                   ApplyDraft()
                                                   Close()
                                               End Sub, 105)
        frame.Controls.Add(DialogStyle.Footer(cancel, _apply, ok), 0, 3)
        AcceptButton = ok
        CancelButton = cancel
        Controls.Add(frame)
        AddHandler _name.TextChanged, Sub()
                                          If Not _filling Then
                                              _draft.Name = _name.Text
                                              MarkDirty()
                                          End If
                                      End Sub
        AddHandler _scope.SelectedIndexChanged, Sub() FillStyle()
        AddHandler _inherit.CheckedChanged, Sub()
                                               If _filling Then Return
                                               Style().Inherit = _inherit.Checked
                                               MarkDirty()
                                               FillStyle()
                                           End Sub
        AddHandler _screens.SelectedIndexChanged, Sub()
                                                     If Not _filling Then
                                                         _draft.ScreenIndex = Math.Max(0, _screens.SelectedIndex)
                                                         MarkDirty()
                                                     End If
                                                 End Sub
        AddHandler _fullscreen.CheckedChanged, Sub()
                                                  If Not _filling Then
                                                      _draft.FullScreen = _fullscreen.Checked
                                                      MarkDirty()
                                                  End If
                                              End Sub
        AddHandler _sound.CheckedChanged, Sub()
                                             If Not _filling Then
                                                 _draft.SoundEnabled = _sound.Checked
                                                 MarkDirty()
                                             End If
                                         End Sub
        AddHandler _buzzDelay.ValueChanged, Sub()
                                                 If _filling Then Return
                                                 _draft.BuzzLampDelayMilliseconds = CInt(_buzzDelay.Value)
                                                 MarkDirty()
                                             End Sub
        AddHandler FormClosing, Sub(sender, e)
                                    If e.CloseReason = CloseReason.UserClosing Then e.Cancel = Not ConfirmPending()
                                End Sub
        _profiles.Items.Add(New ProfileChoice With {.Name = "Текущее: " & _service.Current.Profile.Name})
        Dim catalogErrors As New List(Of String)()
        For Each root In New String() {ApplicationFiles.ProfilesDirectory, Path.Combine(Path.GetDirectoryName(_service.Folder), "profiles")}
            For Each choice In ProfileCatalog.Discover(root, catalogErrors)
                _profiles.Items.Add(choice)
            Next
        Next
        _profiles.SelectedIndex = 0
        AddHandler _profiles.SelectedIndexChanged, Sub() OperatorUi.RunCommand(Me, AddressOf SelectProfile)
        _scope.SelectedIndex = 0
        Fill()
        If catalogErrors.Count > 0 Then _error.Text = String.Join(Environment.NewLine, catalogErrors)
    End Sub

    Private Function Command(text As String, name As String, action As Action, Optional width As Integer = 160) As Button
        Dim button = OperatorUi.Button(Nothing, text, Sub()
                                                         Try
                                                             action()
                                                             _error.Text = ""
                                                         Catch ex As Exception
                                                             _error.Text = ex.Message
                                                         End Try
                                                     End Sub, width)
        button.Name = name
        Return button
    End Function

    Private Function Style() As BoardStyle
        Return _draft.Styles()(Math.Max(0, _scope.SelectedIndex))
    End Function

    Private Sub MarkDirty()
        If _filling Then Return
        _Dirty = Not AppearanceStorage.Equivalent(_draft, _service.Current.Profile)
        _apply.Enabled = Dirty
        DialogStyle.DirtyTitle(Me, "Настройки", Dirty)
    End Sub

    Private Sub Fill()
        _filling = True
        _name.Text = _draft.Name
        _screens.SelectedIndex = Math.Min(_draft.ScreenIndex, _screens.Items.Count - 1)
        _fullscreen.Checked = _draft.FullScreen
        _sound.Checked = _draft.SoundEnabled
        _buzzDelay.Value = _draft.BuzzLampDelayMilliseconds
        _filling = False
        MarkDirty()
        FillStyle()
    End Sub

    Private Sub FillStyle()
        If _scope.SelectedIndex < 0 Then Return
        _filling = True
        Try
            Dim inherited = _scope.SelectedIndex >= 1 AndAlso _scope.SelectedIndex <= 4
            _inherit.Visible = inherited
            _inherit.Checked = inherited AndAlso Style().Inherit
            Dim enabled = Not _inherit.Checked
            Dim shown = If(_inherit.Checked, _draft.Main, Style())
            Dim assets = shown.Assets()
            For i = 0 To 4
                _paths(i).Text = If(assets(i) = "", "По умолчанию", Path.GetFileName(assets(i)))
                _paths(i).AccessibleDescription = assets(i)
                _paths(i).Enabled = enabled AndAlso (i = 0 OrElse _scope.SelectedIndex <> 5)
                _assetButtons(i * 2).Enabled = _paths(i).Enabled
                _assetButtons(i * 2 + 1).Enabled = _paths(i).Enabled
            Next
            _color.Enabled = enabled
            _color.BackColor = Color.FromArgb(shown.TextArgb)
            _color.ForeColor = If(_color.BackColor.GetBrightness() < 0.5F, Color.White, Color.Black)
            _color.UseVisualStyleBackColor = False
            Controls.Find("ResetTextColor", True)(0).Enabled = enabled
        Finally
            _filling = False
        End Try
    End Sub

    Private Sub ChooseAsset(index As Integer)
        Using dialog As New OpenFileDialog With {.Title = "Выберите PNG или JPEG", .Filter = "Изображения (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"}
            If OperatorWindows.ShowFileDialog(dialog, Me) = DialogResult.OK Then SetAsset(index, dialog.FileName)
        End Using
    End Sub

    Public Sub SetAsset(index As Integer, path As String)
        Dim candidate = AppearanceStorage.Clone(_draft)
        candidate.Styles()(_scope.SelectedIndex).SetAsset(index, path)
        Using checked As New LoadedAppearance(candidate)
        End Using
        _draft = candidate
        MarkDirty()
        FillStyle()
    End Sub

    Public Sub SetTextColor(value As Color)
        If value.A <> 255 Then Throw New InvalidDataException("Цвет текста должен быть непрозрачным.")
        Style().TextArgb = value.ToArgb()
        MarkDirty()
        FillStyle()
    End Sub

    Private Sub ChooseColor()
        Using dialog As New ColorDialog With {.Color = Color.FromArgb(Style().TextArgb), .FullOpen = True}
            If OperatorWindows.ShowFileDialog(dialog, Me) <> DialogResult.OK Then Return
            SetTextColor(dialog.Color)
        End Using
    End Sub

    Public Sub ApplyDraft()
        If Not Dirty Then Return
        _service.Apply(_draft)
        _draft = _service.Current.Profile
        _selectingProfile = True
        Try
            _profiles.Items(0) = New ProfileChoice With {.Name = "Текущее: " & _draft.Name}
            _profiles.SelectedIndex = 0
            _selectedProfileIndex = 0
        Finally
            _selectingProfile = False
        End Try
        Fill()
    End Sub

    Public Function ConfirmPending() As Boolean
        If Not CanDiscardDraft() Then Return False
        _draft = _service.Current.Profile
        Fill()
        Return True
    End Function

    Private Function CanDiscardDraft() As Boolean
        Return Not Dirty OrElse DialogStyle.ConfirmDiscard(Me)
    End Function

    Private Sub RestoreProfileSelection()
        _selectingProfile = True
        Try
            _profiles.SelectedIndex = _selectedProfileIndex
        Finally
            _selectingProfile = False
        End Try
    End Sub

    Private Sub SelectProfile()
        If _selectingProfile OrElse _profiles.SelectedIndex < 0 Then Return
        Dim index = _profiles.SelectedIndex
        Try
            Dim choice = DirectCast(_profiles.SelectedItem, ProfileChoice)
            ' Читаем кандидат до решения об отказе от черновика. Даже согласие
            ' на смену не должно стирать поля, если новый профиль повреждён.
            Dim loaded = If(choice.ConfigFile = "", _service.Current.Profile, AppearanceStorage.Open(choice.ConfigFile))
            If Not CanDiscardDraft() Then
                RestoreProfileSelection()
                Return
            End If
            AdoptProfile(loaded)
            _selectedProfileIndex = index
        Catch ex As Exception
            RestoreProfileSelection()
            _error.Text = ex.Message
        End Try
    End Sub

    Private Sub AdoptProfile(loaded As AppearanceProfile)
        loaded.ScreenIndex = _draft.ScreenIndex
        loaded.FullScreen = _draft.FullScreen
        loaded.SoundEnabled = _draft.SoundEnabled
        loaded.BuzzLampDelayMilliseconds = _draft.BuzzLampDelayMilliseconds
        _draft = loaded
        Fill()
        _error.Text = ""
    End Sub

    Private Sub OpenProfileDialog()
        Using dialog As New OpenFileDialog With {.Title = "Открыть config.xml вместе с папкой медиа", .Filter = "Профиль и резервная копия (*.xml;*.bak)|*.xml;*.bak|Все файлы (*.*)|*.*", .InitialDirectory = _service.Folder}
            If OperatorWindows.ShowFileDialog(dialog, Me) <> DialogResult.OK Then Return
            Dim loaded = AppearanceStorage.Open(dialog.FileName)
            If Not CanDiscardDraft() Then Return
            AdoptProfile(loaded)
        End Using
    End Sub

    Private Sub ExportProfile()
        Using dialog As New FolderBrowserDialog With {.Description = "Папка профиля: config.xml и все медиа. Для переноса копируйте папку целиком."}
            If OperatorWindows.ShowFileDialog(dialog, Me) <> DialogResult.OK Then Return
            Dim existing = AppearanceStorage.ConfigPath(dialog.SelectedPath)
            If File.Exists(existing) AndAlso OperatorWindows.Message(Me, "Заменить профиль в выбранной папке?", "Сохранить профиль", MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
            Using exported = AppearanceStorage.Save(_draft, dialog.SelectedPath)
            End Using
            ' Экспорт сохраняет копию черновика, но не применяет его к живым табло.
        End Using
    End Sub

    Private Sub Preview(big As Boolean, splash As Boolean)
        Using loaded As New LoadedAppearance(_draft)
            If big Then
                Using board As New BigGameTablo(True)
                    board.Name = "AppearancePreviewBig"
                    board.ApplyAppearance(loaded)
                    Dim state As New BigGameState()
                    GameRules.ShowBigText(state, 0, "ДЛИННЫЙ ОТВЕТ УЧАСТНИКА")
                    GameRules.ShowBigPoints(state, 0, 45)
                    board.Render(state)
                    board.RenderTimer(17, True)
                    ShowPreview(board)
                End Using
            Else
                Using board As New Tablo(True)
                    board.Name = "AppearancePreviewMain"
                    Dim state As New MainGameState With {.Round = If(_scope.SelectedIndex >= 1 AndAlso _scope.SelectedIndex <= 4, _scope.SelectedIndex, 1), .LeftScore = 123456, .RightScore = 98765}
                    state.LeftStrikes(0) = True
                    state.RightStrikes(2) = True
                    state.SplashScreen.Visible = False
                    GameRules.ShowMainAnswer(state, 0, "ДЛИННЫЙ ОТВЕТ ДЛЯ ПРОВЕРКИ ОФОРМЛЕНИЯ", 45, True)
                    If splash Then GameRules.ShowSplash(state.SplashScreen, "СТО", "К ОДНОМУ")
                    board.ApplyAppearance(loaded, state.Round)
                    board.Render(state)
                    ShowPreview(board)
                End Using
            End If
        End Using
    End Sub

    Private Sub ShowPreview(board As Form)
        board.Text = "Предпросмотр оформления"
        board.ClientSize = New Size(900, 485)
        board.ShowInTaskbar = False
        OperatorWindows.ShowModal(board, Me)
    End Sub
End Class
