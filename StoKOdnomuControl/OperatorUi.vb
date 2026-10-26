Option Strict On
Imports System.Drawing
Imports System.Windows.Forms

' Разметка пультов общая; игровые команды и правила остаются у владельцев окон.
Public NotInheritable Class ConsoleLayout
    Public ReadOnly Toolbar As FlowLayoutPanel
    Public ReadOnly Left As TableLayoutPanel
    Public ReadOnly Right As TableLayoutPanel
    Public ReadOnly Footer As TableLayoutPanel
    Public ReadOnly ErrorLabel As Label
    Public ReadOnly Tips As New ToolTip()

    Public Sub New(form As Form, title As String)
        form.Text = title
        form.Font = New Font("Segoe UI", 10.0F)
        form.AutoScaleMode = AutoScaleMode.Font
        form.ClientSize = New Size(1280, 720)
        form.MinimumSize = New Size(1080, 640)
        form.MaximizeBox = False
        form.StartPosition = FormStartPosition.CenterScreen
        form.BackColor = Color.FromArgb(220, 226, 231)
        Dim frame As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3, .Padding = New Padding(8)}
        frame.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        frame.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        frame.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        frame.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Toolbar = New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .WrapContents = True, .Margin = New Padding(0, 0, 0, 6)}
        frame.Controls.Add(Toolbar, 0, 0)
        Dim scroll As New Panel With {.Name = "ConsoleScroll", .Dock = DockStyle.Fill, .AutoScroll = True, .Margin = New Padding(0)}
        Dim columns As New TableLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .ColumnCount = 2, .RowCount = 1, .Margin = New Padding(0)}
        columns.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        columns.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 310))
        Left = OperatorUi.Stack()
        Right = OperatorUi.Stack()
        Left.Margin = New Padding(0, 0, 8, 0)
        columns.Controls.Add(Left, 0, 0)
        columns.Controls.Add(Right, 1, 0)
        scroll.Controls.Add(columns)
        frame.Controls.Add(scroll, 0, 1)
        ' Нижние команды находятся вне прокрутки. Пустого журнала статусов нет.
        Footer = OperatorUi.Grid(Nothing, 190, 0, 245)
        Footer.Name = "ConsoleFooter"
        Footer.Padding = New Padding(0, 6, 0, 0)
        ErrorLabel = OperatorUi.Label("")
        ErrorLabel.Name = "OperatorError"
        ErrorLabel.ForeColor = Color.DarkRed
        ErrorLabel.Visible = False
        ErrorLabel.Cursor = Cursors.Hand
        Tips.SetToolTip(ErrorLabel, "Нажмите, чтобы скрыть сообщение")
        AddHandler ErrorLabel.Click, Sub() ErrorLabel.Visible = False
        OperatorUi.Cell(Footer, ErrorLabel, 1, 0)
        frame.Controls.Add(Footer, 0, 2)
        form.Controls.Add(frame)
        form.Tag = Me
        AddHandler form.Disposed, Sub() Tips.Dispose()
    End Sub

    Public Sub ShowError(message As String)
        ErrorLabel.Text = message
        ErrorLabel.Visible = Not String.IsNullOrWhiteSpace(message)
        Tips.SetToolTip(ErrorLabel, message & Environment.NewLine & "Нажмите, чтобы скрыть")
    End Sub
End Class

Public Module OperatorUi
    Public Function Stack() As TableLayoutPanel
        Dim panel As New TableLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Dock = DockStyle.Top, .ColumnCount = 1, .RowCount = 0, .Margin = New Padding(0)}
        panel.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        Return panel
    End Function

    Public Sub Append(parent As Control, child As Control)
        Dim table = TryCast(parent, TableLayoutPanel)
        If table Is Nothing Then
            parent.Controls.Add(child)
        Else
            Cell(table, child, 0, table.RowCount)
        End If
    End Sub

    Public Function Group(parent As Control, title As String, name As String) As TableLayoutPanel
        Dim box As New GroupBox With {.Text = title, .Name = name, .Dock = DockStyle.Fill, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Padding = New Padding(8), .Margin = New Padding(0, 0, 0, 8), .BackColor = Color.FromArgb(246, 248, 250)}
        Dim content = Stack()
        content.Dock = DockStyle.Fill
        box.Controls.Add(content)
        If parent IsNot Nothing Then Append(parent, box)
        Return content
    End Function

    Public Function Row(parent As Control) As FlowLayoutPanel
        Dim rowPanel As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .WrapContents = True, .Margin = New Padding(0)}
        Append(parent, rowPanel)
        Return rowPanel
    End Function

    Public Function Label(text As String, Optional width As Integer = 140) As Label
        Return New Label With {.Text = text, .Width = width, .Height = 32, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(3), .AutoEllipsis = True}
    End Function

    Public Sub LargeValue(label As Label)
        Dim ownedFont As New Font("Consolas", 20, FontStyle.Bold)
        label.Font = ownedFont
        label.Height = 38
        Dim fitting As Boolean
        Dim fit As EventHandler = Sub()
                                     If fitting OrElse label.IsDisposed OrElse label.ClientSize.Width < 8 Then Return
                                     fitting = True
                                     Try
                                         Dim width = TextRenderer.MeasureText(label.Text, label.Font, Size.Empty, TextFormatFlags.NoPadding).Width
                                         Dim fittedSize = If(width = 0, 20.0F, Math.Min(20.0F, label.Font.Size * CSng(label.ClientSize.Width - 4) / width))
                                         fittedSize = Math.Max(10.0F, fittedSize)
                                         If Math.Abs(label.Font.Size - fittedSize) > 0.1F Then
                                             Dim replacement As New Font("Consolas", fittedSize, FontStyle.Bold)
                                             Dim previous = ownedFont
                                             label.Font = replacement
                                             ownedFont = replacement
                                             previous.Dispose()
                                         End If
                                     Finally
                                         fitting = False
                                     End Try
                                 End Sub
        ' Шестизначный счёт должен помещаться и после изменения ширины/DPI.
        AddHandler label.TextChanged, fit
        AddHandler label.SizeChanged, fit
        AddHandler label.Disposed, Sub() ownedFont.Dispose()
    End Sub

    Public Function Button(parent As Control, text As String, action As Action, Optional width As Integer = 160) As Button
        Dim command As New Button With {.Text = text, .Width = width, .Height = 36, .MinimumSize = New Size(0, 34), .Margin = New Padding(3), .UseVisualStyleBackColor = True}
        AddHandler command.Click, Sub() RunCommand(command.FindForm(), action)
        If parent IsNot Nothing Then Append(parent, command)
        Return command
    End Function

    Public Sub RunCommand(owner As Form, action As Action)
        Try
            action()
        Catch ex As Exception
            ReportError(owner, ex.Message)
        End Try
    End Sub

    Public Function AsyncButton(parent As Control, text As String, action As Func(Of Threading.Tasks.Task), Optional width As Integer = 160) As Button
        Dim command = Button(parent, text, Sub() Return, width)
        AddHandler command.Click, Async Sub()
                                      Try
                                          Await action()
                                      Catch ex As Exception
                                          ReportError(command.FindForm(), ex.Message)
                                      End Try
                                  End Sub
        Return command
    End Function

    Public Sub ReportError(owner As Form, message As String)
        Dim layout = If(owner Is Nothing, Nothing, TryCast(owner.Tag, ConsoleLayout))
        If layout IsNot Nothing Then
            layout.ShowError(message)
        Else
            MessageBox.Show(owner, message, "Действие не выполнено", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End If
    End Sub

    Public Function Number(Optional maximum As Integer = GameRules.MaximumScore) As NumericUpDown
        Return New NumericUpDown With {.Minimum = 0, .Maximum = maximum, .Width = 80, .Margin = New Padding(3), .ThousandsSeparator = True}
    End Function

    ' 0 — растяжение, положительное число — фиксированная ширина, отрицательное — вес.
    Public Function Grid(parent As Control, ParamArray columns As Single()) As TableLayoutPanel
        Dim table As New TableLayoutPanel With {.AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Dock = DockStyle.Fill, .ColumnCount = columns.Length, .RowCount = 0, .Margin = New Padding(0)}
        For Each width In columns
            table.ColumnStyles.Add(New ColumnStyle(If(width > 0, SizeType.Absolute, SizeType.Percent), If(width = 0, 100, Math.Abs(width))))
        Next
        If parent IsNot Nothing Then Append(parent, table)
        Return table
    End Function

    Public Sub Cell(table As TableLayoutPanel, control As Control, column As Integer, row As Integer)
        While table.RowCount <= row
            table.RowCount += 1
            table.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        End While
        control.Dock = DockStyle.Fill
        table.Controls.Add(control, column, row)
    End Sub

    Public Sub Selected(button As Button, selected As Boolean)
        button.UseVisualStyleBackColor = Not selected
        button.BackColor = If(selected, Color.LightSteelBlue, SystemColors.Control)
    End Sub

    Public Sub EditNumber(owner As Form, title As String, value As Integer, maximum As Integer, apply As Action(Of Integer), Optional minimum As Integer = 0)
        Using dialog As New Form With {.Name = "NumberDialog", .Text = title, .Font = owner.Font, .ClientSize = New Size(440, 125), .FormBorderStyle = FormBorderStyle.FixedDialog, .StartPosition = FormStartPosition.CenterParent, .MinimizeBox = False, .MaximizeBox = False, .ShowInTaskbar = False}
            Dim body = Stack()
            body.Dock = DockStyle.Fill
            body.Padding = New Padding(12)
            Dim row = OperatorUi.Row(body)
            row.Controls.Add(Label("Новое значение", 180))
            Dim number = OperatorUi.Number(maximum)
            number.Name = "NewValue"
            number.Minimum = minimum
            number.Value = value
            row.Controls.Add(number)
            Dim actions = OperatorUi.Row(body)
            Dim ok = Button(actions, "Исправить", Sub() dialog.DialogResult = DialogResult.OK)
            ok.Name = "ApplyNumber"
            Dim cancel = Button(actions, "Отмена", Sub() dialog.DialogResult = DialogResult.Cancel)
            dialog.AcceptButton = ok
            dialog.CancelButton = cancel
            dialog.Controls.Add(body)
            ' До OK числовое поле не связано с игровой моделью или историей отмены.
            If dialog.ShowDialog(owner) = DialogResult.OK Then apply(CInt(number.Value))
        End Using
    End Sub

    Public Function Confirm(owner As Form, text As String) As Boolean
        Return MessageBox.Show(owner, text, "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) = DialogResult.Yes
    End Function

    Public Function ConfirmExit(owner As Form) As Boolean
        Using dialog As New Form With {.Name = "ExitConfirmation", .Text = "Выход", .Font = owner.Font, .ClientSize = New Size(380, 135), .FormBorderStyle = FormBorderStyle.FixedDialog, .StartPosition = FormStartPosition.CenterParent, .MinimizeBox = False, .MaximizeBox = False, .ShowInTaskbar = False}
            Dim body = Stack()
            body.Dock = DockStyle.Fill
            body.Padding = New Padding(12)
            Dim question = Label("Закрыть приложение?")
            question.Name = "ExitQuestion"
            Append(body, question)
            Dim actions = Row(body)
            Dim yes = Button(actions, "Да", Sub() dialog.DialogResult = DialogResult.Yes, 160)
            yes.Name = "ConfirmExitYes"
            Dim no = Button(actions, "Нет", Sub() dialog.DialogResult = DialogResult.No, 160)
            no.Name = "ConfirmExitNo"
            ' По умолчанию выбрано «Нет»; Esc и крестик также отменяют выход.
            dialog.AcceptButton = no
            dialog.CancelButton = no
            dialog.Controls.Add(body)
            dialog.ActiveControl = no
            Return dialog.ShowDialog(owner) = DialogResult.Yes
        End Using
    End Function

    Public Sub InstallUndo(form As Form, undo As Action)
        form.KeyPreview = True
        AddHandler form.KeyDown, Sub(sender, e)
                                    If e.Control AndAlso e.KeyCode = Keys.Z AndAlso Not EditingText(form.ActiveControl) Then
                                        RunCommand(form, undo)
                                        e.SuppressKeyPress = True
                                    End If
                                End Sub
    End Sub

    Public Function EditingText(control As Control) As Boolean
        Dim container = TryCast(control, ContainerControl)
        If container IsNot Nothing AndAlso container.ActiveControl IsNot Nothing Then Return EditingText(container.ActiveControl)
        Return TypeOf control Is TextBoxBase OrElse TypeOf control Is UpDownBase
    End Function

    Public Sub OnUi(form As Form, action As Action)
        If form.IsDisposed OrElse Not form.IsHandleCreated Then Return
        If form.InvokeRequired Then
            Try
                form.BeginInvoke(action)
            Catch ex As InvalidOperationException
                ' Handle мог закрыться между проверкой и BeginInvoke.
            End Try
        Else
            action()
        End If
    End Sub
End Module
