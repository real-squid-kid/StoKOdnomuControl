Option Strict On
Imports System.Drawing.Drawing2D
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Windows.Forms

' Операторские окна не зависят от монитора, выбранного для зрительского табло.
Public Module OperatorWindows
    Public Sub Install(form As Form)
        form.StartPosition = FormStartPosition.Manual
        AddHandler form.Load, Sub() CenterOnPrimary(form)
        AddHandler form.VisibleChanged, Sub()
                                           If form.Visible Then CenterOnPrimary(form)
                                       End Sub
    End Sub

    Public Sub CenterOnPrimary(form As Form)
        Dim area = Screen.PrimaryScreen.WorkingArea
        If form.WindowState <> FormWindowState.Normal Then form.WindowState = FormWindowState.Normal
        form.MinimumSize = New Size(Math.Min(form.MinimumSize.Width, area.Width), Math.Min(form.MinimumSize.Height, area.Height))
        form.Size = New Size(Math.Min(form.Width, area.Width), Math.Min(form.Height, area.Height))
        form.Location = New Point(area.Left + (area.Width - form.Width) \ 2, area.Top + (area.Height - form.Height) \ 2)
    End Sub

    Public Function ShowModal(form As Form, owner As IWin32Window) As DialogResult
        Install(form)
        Return form.ShowDialog(owner)
    End Function

    Public Function ShowFileDialog(dialog As CommonDialog, owner As IWin32Window) As DialogResult
        Using placement As New NativeDialogPlacement()
            Return dialog.ShowDialog(owner)
        End Using
    End Function

    Public Function Message(owner As IWin32Window, text As String, caption As String, buttons As MessageBoxButtons, icon As MessageBoxIcon, Optional defaultButton As MessageBoxDefaultButton = MessageBoxDefaultButton.Button1) As DialogResult
        Using placement As New NativeDialogPlacement()
            Return MessageBox.Show(owner, text, caption, buttons, icon, defaultButton)
        End Using
    End Function
End Module

' MessageBox и системные диалоги файлов/цвета не имеют WinForms.Load.
' Краткоживущий hook действует только в вызывающем UI-потоке и только на #32770;
' окна табло, чужие процессы и пасхалка им не перемещаются.
Friend NotInheritable Class NativeDialogPlacement
    Implements IDisposable
    Private Delegate Function HookProc(code As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    Private ReadOnly _callback As HookProc
    Private _hook As IntPtr
    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativeRect
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer
    End Structure
    <DllImport("user32.dll", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Private Shared Function SetWindowsHookEx(idHook As Integer, callback As HookProc, moduleHandle As IntPtr, threadId As UInteger) As IntPtr
    End Function
    <DllImport("user32.dll")>
    Private Shared Function UnhookWindowsHookEx(hook As IntPtr) As Boolean
    End Function
    <DllImport("user32.dll")>
    Private Shared Function CallNextHookEx(hook As IntPtr, code As Integer, wParam As IntPtr, lParam As IntPtr) As IntPtr
    End Function
    <DllImport("kernel32.dll")>
    Private Shared Function GetCurrentThreadId() As UInteger
    End Function
    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function GetClassName(window As IntPtr, text As StringBuilder, length As Integer) As Integer
    End Function
    <DllImport("user32.dll")>
    Private Shared Function GetWindowRect(window As IntPtr, ByRef bounds As NativeRect) As Boolean
    End Function
    <DllImport("user32.dll")>
    Private Shared Function SetWindowPos(window As IntPtr, insertAfter As IntPtr, x As Integer, y As Integer, width As Integer, height As Integer, flags As UInteger) As Boolean
    End Function

    Public Sub New()
        _callback = AddressOf OnActivate
        _hook = SetWindowsHookEx(5, _callback, IntPtr.Zero, GetCurrentThreadId())
        If _hook = IntPtr.Zero Then Throw New Global.System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error())
    End Sub

    Private Function OnActivate(code As Integer, window As IntPtr, detail As IntPtr) As IntPtr
        If code = 5 Then
            Try
                Dim className As New StringBuilder(64)
                GetClassName(window, className, className.Capacity)
                Dim rect As NativeRect
                If className.ToString() = "#32770" AndAlso GetWindowRect(window, rect) Then
                    Dim area = Screen.PrimaryScreen.WorkingArea
                    SetWindowPos(window, IntPtr.Zero, area.Left + Math.Max(0, (area.Width - rect.Right + rect.Left) \ 2), area.Top + Math.Max(0, (area.Height - rect.Bottom + rect.Top) \ 2), 0, 0, &H15UI)
                End If
            Catch ex As Exception
                Diagnostics.Debug.WriteLine(ex.Message)
            End Try
        End If
        Return CallNextHookEx(_hook, code, window, detail)
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If _hook <> IntPtr.Zero Then UnhookWindowsHookEx(_hook)
        _hook = IntPtr.Zero
        GC.KeepAlive(_callback)
    End Sub
End Class

' Нативная рамка Windows с мягкой панелью заголовка и заметной меткой черновика.
Public NotInheritable Class DialogHeading
    Inherits Control
    Private _dirty As Boolean
    Public Sub New(title As String)
        Name = "DialogHeading"
        Text = title
        Dock = DockStyle.Fill
        Height = 48
        SetStyle(ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        Font = New Font("Segoe UI", 15, FontStyle.Regular)
        AddHandler Disposed, Sub() Font.Dispose()
    End Sub
    Public Sub SetDirty(dirty As Boolean)
        _dirty = dirty
        AccessibleDescription = If(dirty, "Есть несохраненные изменения", "Изменения сохранены")
        Invalidate()
    End Sub
    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Using brush As New LinearGradientBrush(ClientRectangle, Color.White, Color.FromArgb(228, 236, 243), 90.0F)
            e.Graphics.FillRectangle(brush, ClientRectangle)
        End Using
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        If _dirty Then e.Graphics.FillEllipse(Brushes.SteelBlue, 14, (Height - 16) \ 2, 16, 16)
        TextRenderer.DrawText(e.Graphics, Text, Font, New Rectangle(If(_dirty, 40, 14), 0, Width - 45, Height), Color.FromArgb(35, 52, 70), TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)
    End Sub
End Class

Public Module DialogStyle
    Public Function Footer(cancel As Button, apply As Button, ok As Button) As TableLayoutPanel
        Dim panel = OperatorUi.Grid(Nothing, 135, 0, 155, 110)
        panel.Name = "SettingsFooter"
        panel.AutoSize = False
        panel.Height = 50
        panel.Padding = New Padding(3)
        panel.BackColor = Color.FromArgb(232, 237, 242)
        OperatorUi.Cell(panel, cancel, 0, 0)
        OperatorUi.Cell(panel, apply, 2, 0)
        OperatorUi.Cell(panel, ok, 3, 0)
        Return panel
    End Function

    Public Sub DirtyTitle(form As Form, title As String, dirty As Boolean)
        form.Text = If(dirty, "● ", "") & title
        Dim heading = form.Controls.Find("DialogHeading", True).OfType(Of DialogHeading)().FirstOrDefault()
        If heading IsNot Nothing Then heading.SetDirty(dirty)
    End Sub

    Public Function ConfirmDiscard(owner As Form) As Boolean
        Using dialog As New Form With {.Name = "UnsavedChanges", .Text = "Есть несохраненные изменения", .Font = owner.Font, .ClientSize = New Size(570, 160), .FormBorderStyle = FormBorderStyle.FixedDialog, .MaximizeBox = False, .MinimizeBox = False, .ShowInTaskbar = False}
            Dim body = OperatorUi.Stack()
            body.Padding = New Padding(14)
            body.Dock = DockStyle.Fill
            Dim heading As New DialogHeading("Есть несохраненные изменения")
            heading.SetDirty(True)
            OperatorUi.Append(body, heading)
            Dim actions = OperatorUi.Grid(body, -1, -1)
            Dim review = OperatorUi.Button(Nothing, "Перепроверить", Sub() dialog.DialogResult = DialogResult.Cancel)
            review.Name = "ReviewChanges"
            Dim discard = OperatorUi.Button(Nothing, "Все равно закрыть", Sub() dialog.DialogResult = DialogResult.No)
            discard.Name = "DiscardChanges"
            OperatorUi.Cell(actions, review, 0, 0)
            OperatorUi.Cell(actions, discard, 1, 0)
            dialog.Controls.Add(body)
            dialog.AcceptButton = review
            dialog.CancelButton = review
            dialog.ActiveControl = review
            Return OperatorWindows.ShowModal(dialog, owner) = DialogResult.No
        End Using
    End Function
End Module
