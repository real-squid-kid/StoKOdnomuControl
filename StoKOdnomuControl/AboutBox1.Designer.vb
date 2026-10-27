<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class AboutBox1
    Inherits System.Windows.Forms.Form

    Private components As System.ComponentModel.IContainer
    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then components.Dispose()
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.TableLayoutPanel = New System.Windows.Forms.TableLayoutPanel()
        Me.LabelProductName = New System.Windows.Forms.Label()
        Me.LabelVersion = New System.Windows.Forms.Label()
        Me.LabelCopyright = New System.Windows.Forms.Label()
        Me.LabelCompanyName = New System.Windows.Forms.Label()
        Me.TextBoxDescription = New System.Windows.Forms.TextBox()
        Me.OKButton = New System.Windows.Forms.Button()
        Me.RunLbl = New System.Windows.Forms.Label()
        Me.SuspendLayout()
        Me.Font = New System.Drawing.Font("Segoe UI", 10.0!)
        Me.BackColor = System.Drawing.Color.FromArgb(246, 248, 250)
        Me.ClientSize = New System.Drawing.Size(660, 360)
        Me.Padding = New System.Windows.Forms.Padding(18)
        Me.TableLayoutPanel.ColumnCount = 1
        Me.TableLayoutPanel.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.TableLayoutPanel.RowCount = 6
        Me.TableLayoutPanel.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48.0!))
        Me.TableLayoutPanel.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30.0!))
        Me.TableLayoutPanel.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 52.0!))
        Me.TableLayoutPanel.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30.0!))
        Me.TableLayoutPanel.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        Me.TableLayoutPanel.RowStyles.Add(New System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46.0!))
        Me.TableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill
        Me.TableLayoutPanel.Name = "AboutLayout"
        Me.LabelProductName.Name = "LabelProductName"
        Me.LabelProductName.Font = New System.Drawing.Font("Segoe UI", 19.0!)
        Me.LabelProductName.ForeColor = System.Drawing.Color.FromArgb(35, 52, 70)
        Me.LabelVersion.Name = "LabelVersion"
        Me.LabelVersion.ForeColor = System.Drawing.Color.DimGray
        Me.LabelCopyright.Name = "LabelCopyright"
        Me.LabelCompanyName.Name = "LabelCompanyName"
        For Each label As System.Windows.Forms.Label In New System.Windows.Forms.Label() {Me.LabelProductName, Me.LabelVersion, Me.LabelCopyright, Me.LabelCompanyName}
            label.Dock = System.Windows.Forms.DockStyle.Fill
            label.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        Next
        Me.TextBoxDescription.Name = "TextBoxDescription"
        Me.TextBoxDescription.Dock = System.Windows.Forms.DockStyle.Fill
        Me.TextBoxDescription.ReadOnly = True
        Me.TextBoxDescription.Multiline = True
        Me.TextBoxDescription.TabStop = False
        Me.TextBoxDescription.BorderStyle = System.Windows.Forms.BorderStyle.None
        Me.TextBoxDescription.BackColor = Me.BackColor
        Me.TextBoxDescription.Margin = New System.Windows.Forms.Padding(3, 10, 3, 3)
        Dim footer As New System.Windows.Forms.TableLayoutPanel With {.Dock = System.Windows.Forms.DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .Margin = New System.Windows.Forms.Padding(0)}
        footer.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100.0!))
        footer.ColumnStyles.Add(New System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110.0!))
        Me.RunLbl.Name = "RunLbl"
        Me.RunLbl.Text = "Run..."
        Me.RunLbl.AutoSize = False
        Me.RunLbl.Size = New System.Drawing.Size(100, 40)
        Me.RunLbl.TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        Me.RunLbl.Cursor = System.Windows.Forms.Cursors.Hand
        Me.RunLbl.Padding = New System.Windows.Forms.Padding(8, 0, 0, 0)
        Me.RunLbl.AccessibleName = "Run..."
        Me.OKButton.Name = "OKButton"
        Me.OKButton.Text = "ОК"
        Me.OKButton.DialogResult = System.Windows.Forms.DialogResult.OK
        Me.OKButton.Dock = System.Windows.Forms.DockStyle.Fill
        Me.OKButton.UseVisualStyleBackColor = True
        footer.Controls.Add(Me.RunLbl, 0, 0)
        footer.Controls.Add(Me.OKButton, 1, 0)
        Me.TableLayoutPanel.Controls.Add(Me.LabelProductName, 0, 0)
        Me.TableLayoutPanel.Controls.Add(Me.LabelVersion, 0, 1)
        Me.TableLayoutPanel.Controls.Add(Me.LabelCopyright, 0, 2)
        Me.TableLayoutPanel.Controls.Add(Me.LabelCompanyName, 0, 3)
        Me.TableLayoutPanel.Controls.Add(Me.TextBoxDescription, 0, 4)
        Me.TableLayoutPanel.Controls.Add(footer, 0, 5)
        Me.Controls.Add(Me.TableLayoutPanel)
        Me.AcceptButton = Me.OKButton
        Me.CancelButton = Me.OKButton
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "AboutBox1"
        Me.ShowInTaskbar = False
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ResumeLayout(False)
    End Sub

    Friend WithEvents TableLayoutPanel As System.Windows.Forms.TableLayoutPanel
    Friend WithEvents LabelProductName As System.Windows.Forms.Label
    Friend WithEvents LabelVersion As System.Windows.Forms.Label
    Friend WithEvents LabelCopyright As System.Windows.Forms.Label
    Friend WithEvents LabelCompanyName As System.Windows.Forms.Label
    Friend WithEvents TextBoxDescription As System.Windows.Forms.TextBox
    Friend WithEvents OKButton As System.Windows.Forms.Button
    Friend WithEvents RunLbl As System.Windows.Forms.Label
End Class
