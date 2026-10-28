Option Strict On
Imports System.ComponentModel
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

' Проверяем системную настройку каждого подключённого монитора, а не DpiX
' виртуализированного окна. Предупреждение не меняет масштаб или DPI-awareness.
Friend Module DisplayScaleWarning
    Public Sub ShowIfNeeded(owner As Form)
        Dim message As String
        Try
            message = BuildMessage(ReadScales())
        Catch ex As Exception When TypeOf ex Is ExternalException OrElse TypeOf ex Is DllNotFoundException OrElse TypeOf ex Is EntryPointNotFoundException OrElse TypeOf ex Is Win32Exception
            ' Неудача диагностического запроса не должна препятствовать запуску матча.
            Diagnostics.Debug.WriteLine(ex.ToString())
            Return
        End Try
        If message = "" Then Return
        Using dialog = CreateDialog(message)
            OperatorWindows.ShowModal(dialog, owner)
        End Using
    End Sub

    Friend Function BuildMessage(scales As IDictionary(Of String, Integer)) As String
        Dim changed = scales.Where(Function(pair) pair.Value <> 100).Select(Function(pair) pair.Key & ": " & pair.Value & "%").ToArray()
        If changed.Length = 0 Then Return ""
        Return "На подключённых экранах установлен масштаб Windows, отличный от 100%:" & Environment.NewLine &
            String.Join(Environment.NewLine, changed) & Environment.NewLine & Environment.NewLine &
            "При таком масштабе элементы пульта и табло могут отображаться некорректно." & Environment.NewLine & Environment.NewLine &
            "Где изменить: Пуск → Параметры → Система → Дисплей (Экран). Выберите нужный экран и в разделе «Масштаб и разметка» установите «Масштаб» 100%. Повторите для остальных экранов и перезапустите приложение." & Environment.NewLine & Environment.NewLine &
            "Разрешение экрана менять не требуется."
    End Function

    Friend Function CreateDialog(message As String) As Form
        Dim dialog As New Form With {.Name = "DisplayScaleWarning", .Text = "Масштаб Windows", .Font = New Font("Segoe UI", 10), .ClientSize = New Size(700, 390), .FormBorderStyle = FormBorderStyle.FixedDialog, .MaximizeBox = False, .MinimizeBox = False, .ShowInTaskbar = False}
        Dim frame As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 3, .Padding = New Padding(14)}
        frame.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        frame.RowStyles.Add(New RowStyle(SizeType.Absolute, 55))
        frame.RowStyles.Add(New RowStyle(SizeType.Percent, 100))
        frame.RowStyles.Add(New RowStyle(SizeType.Absolute, 52))
        frame.Controls.Add(New DialogHeading("Рекомендуется масштаб 100%"), 0, 0)
        Dim body As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True}
        body.Controls.Add(New Label With {.Name = "ScaleWarningText", .Text = message, .AutoSize = True, .MaximumSize = New Size(640, 0), .Location = New Point(3, 10)})
        frame.Controls.Add(body, 0, 1)
        Dim footer As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .FlowDirection = FlowDirection.RightToLeft}
        Dim proceed = OperatorUi.Button(Nothing, "Продолжить", Sub() dialog.DialogResult = DialogResult.OK, 150)
        proceed.Name = "ContinueScaleWarning"
        footer.Controls.Add(proceed)
        frame.Controls.Add(footer, 0, 2)
        dialog.Controls.Add(frame)
        dialog.AcceptButton = proceed
        dialog.CancelButton = proceed
        Return dialog
    End Function

    Friend Function ReadScales() As Dictionary(Of String, Integer)
        Dim scales As New Dictionary(Of String, Integer)()
        Dim failure As Exception = Nothing
        Dim screens = Screen.AllScreens
        Dim callback As MonitorEnumProc = Function(monitor As IntPtr, dc As IntPtr, ByRef rect As NativeRect, data As IntPtr) As Boolean
                                              Try
                                                  Dim info As New MonitorInfo With {.Size = Marshal.SizeOf(GetType(MonitorInfo))}
                                                  If Not GetMonitorInfo(monitor, info) Then Throw New Win32Exception(Marshal.GetLastWin32Error())
                                                  Dim scale As Integer
                                                  Marshal.ThrowExceptionForHR(GetScaleFactorForMonitor(monitor, scale))
                                                  Dim index = Array.FindIndex(screens, Function(screen) screen.DeviceName = info.DeviceName)
                                                  Dim name = If(index >= 0, "Экран " & (index + 1), info.DeviceName)
                                                  If (info.Flags And 1) <> 0 Then name &= " (основной)"
                                                  scales.Add(name, scale)
                                                  Return True
                                              Catch ex As Exception
                                                  ' Исключение не должно пересекать границу unmanaged callback.
                                                  failure = ex
                                                  Return False
                                              End Try
                                          End Function
        Dim success = EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero)
        Dim errorCode = Marshal.GetLastWin32Error()
        GC.KeepAlive(callback)
        If failure IsNot Nothing Then Throw failure
        If Not success Then Throw New Win32Exception(errorCode)
        Return scales
    End Function

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativeRect
        Public Left, Top, Right, Bottom As Integer
    End Structure
    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
    Private Structure MonitorInfo
        Public Size As Integer
        Public Bounds As NativeRect
        Public WorkArea As NativeRect
        Public Flags As Integer
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=32)>
        Public DeviceName As String
    End Structure
    Private Delegate Function MonitorEnumProc(monitor As IntPtr, dc As IntPtr, ByRef rect As NativeRect, data As IntPtr) As Boolean
    <DllImport("user32.dll", SetLastError:=True)>
    Private Function EnumDisplayMonitors(dc As IntPtr, clip As IntPtr, callback As MonitorEnumProc, data As IntPtr) As Boolean
    End Function
    <DllImport("user32.dll", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Private Function GetMonitorInfo(monitor As IntPtr, ByRef info As MonitorInfo) As Boolean
    End Function
    <DllImport("shcore.dll")>
    Private Function GetScaleFactorForMonitor(monitor As IntPtr, ByRef scale As Integer) As Integer
    End Function
End Module
