Option Strict On
Imports System.Windows.Forms

' Поля являются черновиком; только «Показать» вызывает команду игровой сессии.
Public NotInheritable Class SplashEditor
    Inherits TableLayoutPanel
    Private ReadOnly _presets As New ComboBox With {.Name = "SplashPresets", .DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly _line1 As New TextBox With {.Name = "SplashLine1", .MaxLength = GameRules.MaximumSplashLineLength}
    Private ReadOnly _line2 As New TextBox With {.Name = "SplashLine2", .MaxLength = GameRules.MaximumSplashLineLength}
    Private ReadOnly _return As Button
    Private _last As SplashState
    Private _updating As Boolean

    Public Sub New(publish As Action(Of String, String), returnAnswers As Action, preview As Action(Of String, String))
        AutoSize = True
        AutoSizeMode = AutoSizeMode.GrowAndShrink
        Dock = DockStyle.Fill
        ColumnCount = 2
        RowCount = 0
        ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 70))
        ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        _presets.Items.AddRange(SplashPresets.Names.Cast(Of Object)().ToArray())
        OperatorUi.Cell(Me, _presets, 0, 0)
        SetColumnSpan(_presets, 2)
        OperatorUi.Cell(Me, OperatorUi.Label("Строка 1"), 0, 1)
        OperatorUi.Cell(Me, _line1, 1, 1)
        OperatorUi.Cell(Me, OperatorUi.Label("Строка 2"), 0, 2)
        OperatorUi.Cell(Me, _line2, 1, 2)
        Dim localPreview = OperatorUi.Button(Nothing, "Предпросмотр…", Sub() preview(_line1.Text, _line2.Text))
        localPreview.Name = "PreviewSplash"
        Dim commands = OperatorUi.Grid(Nothing, -1, -1)
        OperatorUi.Cell(Me, commands, 0, 3)
        SetColumnSpan(commands, 2)
        OperatorUi.Cell(commands, localPreview, 0, 0)
        Dim show = OperatorUi.Button(Nothing, "Показать заставку", Sub() publish(_line1.Text, _line2.Text))
        show.Name = "ShowSplash"
        OperatorUi.Cell(commands, show, 1, 0)
        _return = OperatorUi.Button(Nothing, "Вернуть ответы", returnAnswers)
        _return.Name = "ReturnAnswers"
        OperatorUi.Cell(Me, _return, 0, 4)
        SetColumnSpan(_return, 2)
        AddHandler _presets.SelectedIndexChanged, Sub()
                                                     If _updating OrElse _presets.SelectedIndex < 0 Then Return
                                                     Dim preset = SplashPresets.Create(_presets.SelectedIndex)
                                                     _updating = True
                                                     _line1.Text = preset.Line1
                                                     _line2.Text = preset.Line2
                                                     _updating = False
                                                 End Sub
        AddHandler _line1.TextChanged, AddressOf DraftChanged
        AddHandler _line2.TextChanged, AddressOf DraftChanged
    End Sub

    Private Sub DraftChanged(sender As Object, e As EventArgs)
        If _updating Then Return
        _updating = True
        _presets.SelectedIndex = SplashPresets.Find(_line1.Text, _line2.Text)
        _updating = False
    End Sub

    Public Sub Render(state As SplashState)
        ' Смена счёта, таймера или видимости не стирает ещё не опубликованный текст.
        If _last Is Nothing OrElse _last.Line1 <> state.Line1 OrElse _last.Line2 <> state.Line2 Then
            _updating = True
            _line1.Text = state.Line1
            _line2.Text = state.Line2
            _presets.SelectedIndex = SplashPresets.Find(state.Line1, state.Line2)
            _updating = False
        End If
        _return.Enabled = state.Visible
        _last = state
    End Sub

    Public Sub ReloadDrafts()
        _last = Nothing
    End Sub
End Class
