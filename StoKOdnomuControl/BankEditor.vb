Option Strict On
Imports System.IO
Imports System.Windows.Forms

Public NotInheritable Class BankEditor
    Inherits Form
    Private _document As New BankDocument()
    Private ReadOnly _loadBank As Action(Of String)
    Private ReadOnly _tabs As New TabControl With {.Dock = DockStyle.Fill, .Name = "BankRounds"}
    Private ReadOnly _texts(3, 5) As TextBox
    Private ReadOnly _points(3, 5) As NumericUpDown
    Private ReadOnly _awards(3, 5) As Label
    Private ReadOnly _load As Button
    Private ReadOnly _error As Label = OperatorUi.Label("")
    Private ReadOnly _apply As Button
    Private _filling As Boolean
    Public ReadOnly Property Dirty As Boolean
    Public ReadOnly Property Document As BankDocument
        Get
            Return _document
        End Get
    End Property

    Public Sub New(loadBank As Action(Of String))
        _loadBank = loadBank
        Name = "BankEditor"
        Text = "Редактор базы"
        Font = New Font("Segoe UI", 10)
        ClientSize = New Size(1010, 620)
        MinimumSize = New Size(900, 570)
        OperatorWindows.Install(Me)
        Dim frame As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 5, .Padding = New Padding(10)}
        frame.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        frame.RowStyles.Add(New RowStyle(SizeType.Absolute, 52))
        frame.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        frame.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        frame.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        frame.RowStyles.Add(New RowStyle(SizeType.Absolute, 58))
        Dim commands As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill}
        AddCommand(commands, "Новая база", "NewBank", AddressOf NewDocument, 130)
        AddCommand(commands, "Открыть…", "OpenBank", AddressOf OpenDialog, 115)
        AddCommand(commands, "Сохранить как…", "SaveBankAs", Sub() SaveDialog(True), 160)
        _load = AddCommand(commands, "Загрузить в пульт", "LoadBankToConsole", Sub() _loadBank(_document.FilePath), 180)
        frame.Controls.Add(New DialogHeading("Редактор базы"), 0, 0)
        frame.Controls.Add(commands, 0, 1)
        frame.Controls.Add(_tabs, 0, 2)
        For r = 0 To 3
            Dim round = r
            Dim tab As New TabPage(BankDocument.RoundName(r) & If(r < 3, " · ×" & (r + 1), "")) With {.AutoScroll = True, .Padding = New Padding(10)}
            Dim table = OperatorUi.Grid(Nothing, 35, 0, 125, 140)
            table.Dock = DockStyle.Top
            For c = 0 To 3
                OperatorUi.Cell(table, OperatorUi.Label(New String() {"№", "Ответ", If(r < 3, "Ответили, чел.", "Цена"), "В банк, очков"}(c)), c, 0)
            Next
            For i = 0 To 5
                Dim index = i
                _texts(r, i) = New TextBox With {.Name = "BankAnswer" & r & "_" & i, .MaxLength = 120, .Margin = New Padding(3, 8, 3, 8)}
                _points(r, i) = OperatorUi.Number(GameRules.MaximumAnswerPoints)
                _points(r, i).Name = "BankPeople" & r & "_" & i
                _points(r, i).Enabled = r < 3
                _awards(r, i) = OperatorUi.Label("0")
                _awards(r, i).Name = "BankAward" & r & "_" & i
                AddHandler _texts(r, i).TextChanged, Sub() MarkDirty()
                AddHandler _points(r, i).ValueChanged, Sub()
                                                          _awards(round, index).Text = (CInt(_points(round, index).Value) * RoundRules.Multiplier(round + 1)).ToString()
                                                          MarkDirty()
                                                      End Sub
                OperatorUi.Cell(table, OperatorUi.Label((i + 1).ToString()), 0, i + 1)
                OperatorUi.Cell(table, _texts(r, i), 1, i + 1)
                OperatorUi.Cell(table, _points(r, i), 2, i + 1)
                OperatorUi.Cell(table, _awards(r, i), 3, i + 1)
            Next
            tab.Controls.Add(table)
            _tabs.TabPages.Add(tab)
        Next
        _error.Name = "BankError"
        _error.ForeColor = Color.DarkRed
        _error.AutoSize = True
        _error.MaximumSize = New Size(950, 0)
        frame.Controls.Add(_error, 0, 3)
        Dim cancel = AddCommand(Nothing, "Отмена", "CancelBankEditor", AddressOf Close, 130)
        _apply = AddCommand(Nothing, "Применить", "ApplyBankEditor", Sub() SaveDialog(False), 150)
        Dim ok = AddCommand(Nothing, "ОК", "OkBankEditor", Sub()
                                                                                 If Not Dirty OrElse SaveDialog(False) Then Close()
                                                                             End Sub, 105)
        frame.Controls.Add(DialogStyle.Footer(cancel, _apply, ok), 0, 4)
        AcceptButton = ok
        CancelButton = cancel
        Controls.Add(frame)
        AddHandler FormClosing, Sub(sender, e)
                                    If e.CloseReason = CloseReason.UserClosing Then e.Cancel = Not ConfirmPending()
                                End Sub
        Fill()
    End Sub

    Private Function AddCommand(parent As FlowLayoutPanel, text As String, name As String, action As Action, width As Integer) As Button
        Dim button = OperatorUi.Button(Nothing, text, Sub()
                                                         Try
                                                             action()
                                                             _error.Text = ""
                                                         Catch ex As Exception
                                                             _error.Text = ex.Message
                                                         End Try
                                                     End Sub, width)
        button.Name = name
        If parent IsNot Nothing Then parent.Controls.Add(button)
        Return button
    End Function

    Private Sub MarkDirty()
        If _filling Then Return
        _Dirty = False
        For r = 0 To 3
            For i = 0 To 5
                If _texts(r, i).Text <> _document.Bank.Rounds(r)(i).Text OrElse CInt(_points(r, i).Value) <> _document.Bank.Rounds(r)(i).Points Then _Dirty = True
            Next
        Next
        RefreshCaption()
    End Sub

    Private Sub RefreshCaption()
        DialogStyle.DirtyTitle(Me, "Редактор базы — " & If(_document.FilePath = "", "Новая база", Path.GetFileName(_document.FilePath)), Dirty)
        _apply.Enabled = Dirty
        _load.Enabled = Not Dirty AndAlso _document.FilePath <> "" AndAlso _loadBank IsNot Nothing
    End Sub

    Private Sub Fill()
        _filling = True
        Try
            For r = 0 To 3
                For i = 0 To 5
                    _texts(r, i).Text = _document.Bank.Rounds(r)(i).Text
                    _points(r, i).Value = _document.Bank.Rounds(r)(i).Points
                    _awards(r, i).Text = (CInt(_points(r, i).Value) * RoundRules.Multiplier(r + 1)).ToString()
                Next
            Next
            _Dirty = False
            _error.Text = ""
            RefreshCaption()
        Finally
            _filling = False
        End Try
    End Sub

    Public Sub OpenDocument(path As String)
        Dim loaded = BankDocument.Open(path)
        _document = loaded
        Fill()
    End Sub

    Public Sub SaveDocument(path As String)
        ValidateChildren()
        Dim candidate = BankDocument.EmptyBank()
        For r = 0 To 3
            For i = 0 To 5
                candidate.Rounds(r)(i).Text = _texts(r, i).Text
                candidate.Rounds(r)(i).Points = CInt(_points(r, i).Value)
                ' Адресуем первую ошибку вкладке и полю, не стирая остальные строки.
                Try
                    GameRules.CheckAnswer(_texts(r, i).Text, CInt(_points(r, i).Value))
                    If r < 3 AndAlso _texts(r, i).Text.Contains("&") Then Throw New InvalidOperationException("Уберите & из ответа.")
                Catch ex As InvalidOperationException
                    _tabs.SelectedIndex = r
                    _texts(r, i).Focus()
                    Throw New InvalidDataException("Раунд «" & BankDocument.RoundName(r) & "», строка " & (i + 1) & ": " & ex.Message, ex)
                End Try
            Next
        Next
        _document.Save(path, candidate)
        Fill()
    End Sub

    Private Function SaveDialog(saveAs As Boolean) As Boolean
        Dim path = _document.FilePath
        If saveAs OrElse path = "" Then
            Using dialog As New SaveFileDialog With {.Title = "Сохранить базу", .Filter = "База ответов (*.txt)|*.txt", .DefaultExt = "txt", .FileName = If(path = "", "base.txt", path)}
                If OperatorWindows.ShowFileDialog(dialog, Me) <> DialogResult.OK Then Return False
                path = dialog.FileName
            End Using
        End If
        Try
            SaveDocument(path)
            Return True
        Catch ex As BankConflictException
            If OperatorWindows.Message(Me, ex.Message & Environment.NewLine & "Перечитать файл с диска? Текущий черновик будет заменён. Выберите «Нет», чтобы оставить его и сохранить под другим именем.", "Файл изменён", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then OpenDocument(_document.FilePath)
            Return False
        End Try
    End Function

    Public Function ConfirmPending() As Boolean
        If Not Dirty Then Return True
        If Not DialogStyle.ConfirmDiscard(Me) Then Return False
        Fill()
        Return True
    End Function

    Private Sub NewDocument()
        If Not ConfirmPending() Then Return
        _document = New BankDocument()
        Fill()
    End Sub

    Private Sub OpenDialog()
        If Not ConfirmPending() Then Return
        Using dialog As New OpenFileDialog With {.Filter = "База и резервная копия (*.txt;*.bak)|*.txt;*.bak|Все файлы (*.*)|*.*"}
            If OperatorWindows.ShowFileDialog(dialog, Me) = DialogResult.OK Then OpenDocument(dialog.FileName)
        End Using
    End Sub
End Class
