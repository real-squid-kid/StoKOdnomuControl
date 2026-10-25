Option Strict On
Imports System.Drawing
Imports System.Windows.Forms

Public Module OperatorUi
    Public Function Page(form As Form, title As String) As FlowLayoutPanel
        form.Text = title
        form.Font = New Font("Segoe UI", 10.0F)
        form.BackColor = Color.FromArgb(242, 245, 249)
        form.ClientSize = New Size(1264, 850)
        form.MinimumSize = New Size(1080, 640)
        form.StartPosition = FormStartPosition.CenterScreen
        Dim scroll As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .Padding = New Padding(12)}
        Dim content As New FlowLayoutPanel With {.Dock = DockStyle.Top, .AutoSize = True, .FlowDirection = FlowDirection.TopDown, .WrapContents = False}
        scroll.Controls.Add(content)
        form.Controls.Add(scroll)
        AddHandler scroll.SizeChanged, Sub()
                                          For Each child As Control In content.Controls
                                              child.Width = Math.Max(1000, scroll.ClientSize.Width - 42)
                                          Next
                                      End Sub
        Return content
    End Function

    Public Function Row(parent As Control) As FlowLayoutPanel
        Dim panel As New FlowLayoutPanel With {.AutoSize = True, .WrapContents = True, .Width = 1210, .Padding = New Padding(4), .Margin = New Padding(0, 2, 0, 2)}
        parent.Controls.Add(panel)
        Return panel
    End Function

    Public Function Label(text As String, Optional width As Integer = 250) As Label
        Return New Label With {.Text = text, .AutoSize = False, .Width = width, .Height = 34, .TextAlign = ContentAlignment.MiddleLeft, .Margin = New Padding(4)}
    End Function

    Public Function Heading(parent As Control, text As String) As Label
        Dim label = OperatorUi.Label(text, 1200)
        label.Font = New Font("Segoe UI", 15, FontStyle.Bold)
        label.Height = 42
        parent.Controls.Add(label)
        Return label
    End Function

    Public Function Button(parent As Control, text As String, action As Action, Optional width As Integer = 190) As Button
        Dim commandButton As New Button With {.Text = text, .Width = width, .Height = 40, .Margin = New Padding(4), .UseVisualStyleBackColor = True}
        AddHandler commandButton.Click, Sub()
                                     Try
                                         action()
                                     Catch ex As Exception
                                         MessageBox.Show(parent.FindForm(), ex.Message, "Действие не выполнено", MessageBoxButtons.OK, MessageBoxIcon.Information)
                                     End Try
                                 End Sub
        parent.Controls.Add(commandButton)
        Return commandButton
    End Function

    Public Function Number(Optional maximum As Integer = GameRules.MaximumScore) As NumericUpDown
        Return New NumericUpDown With {.Minimum = 0, .Maximum = maximum, .Width = 95, .Height = 32, .Margin = New Padding(4, 9, 4, 4), .ThousandsSeparator = True}
    End Function

    Public Function Grid(parent As Control, ParamArray columns As Single()) As TableLayoutPanel
        Dim table As New TableLayoutPanel With {.ColumnCount = columns.Length, .AutoSize = True, .Width = 1210, .Padding = New Padding(4), .Margin = New Padding(0, 4, 0, 4), .BackColor = Color.White, .GrowStyle = TableLayoutPanelGrowStyle.AddRows}
        For Each width In columns
            table.ColumnStyles.Add(New ColumnStyle(If(width = 0, SizeType.Percent, SizeType.Absolute), If(width = 0, 100, width)))
        Next
        parent.Controls.Add(table)
        Return table
    End Function

    Public Sub Cell(table As TableLayoutPanel, control As Control, column As Integer, row As Integer)
        table.Controls.Add(control, column, row)
        If TypeOf control Is TextBox Then control.Anchor = AnchorStyles.Left Or AnchorStyles.Right
    End Sub

    Public Function Confirm(owner As Form, text As String) As Boolean
        Return MessageBox.Show(owner, text & Environment.NewLine & "Действие можно отменить кнопкой «Отменить».", "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) = DialogResult.Yes
    End Function

    Public Sub OnUi(form As Form, action As Action)
        If form.IsDisposed OrElse Not form.IsHandleCreated Then Return
        If form.InvokeRequired Then
            Try
                form.BeginInvoke(action)
            Catch ex As InvalidOperationException
                ' A pending device callback may arrive while the operator window closes.
            End Try
        Else
            action()
        End If
    End Sub
End Module
