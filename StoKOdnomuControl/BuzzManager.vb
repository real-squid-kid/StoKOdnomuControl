Imports System.Runtime.InteropServices
Imports System.Threading

Public Class BuzzManager
    Implements IDisposable

    Public Event BuzzerPressed(ByVal padIndex As Integer)

    ' >>> ПРОВЕРЬТЕ: VID/PID вашего приёмника (Диспетчер устройств -> ИД оборудования)
    Private Const BuzzVid As Short = &H54C   ' Sony
    Private Const BuzzPid As Short = &H1000
    Private Const DeviceNameFilter As String = "Wbuzz"

    Private Const PollMs As Integer = 20
    Private Const BlinkMs As Integer = 500
    Private Const ActiveWindowSec As Double = 15

    ' Кнопки 5, 10, 15, 20 -> биты 4, 9, 14, 19
    Private Shared ReadOnly BuzzerMasks As Integer() = {1 << 4, 1 << 9, 1 << 14, 1 << 19}

    Private _hid As IntPtr = InvalidHandle
    Private _joyId As Integer = -1
    Private _pollTimer As Timer
    Private _blinkTimer As Timer
    Private _lastButtons As Integer
    Private _activePad As Integer = -1
    Private _blinkOn As Boolean
    Private _activeSince As DateTime
    Private ReadOnly _hidLock As New Object()
    Private ReadOnly _stateLock As New Object()

    Public Function Initialize() As Boolean
        _hid = FindHidDevice(BuzzVid, BuzzPid)
        If _hid = InvalidHandle Then Return False

        If Not SendCommand(New Byte(7) {}) Then Return False   ' инициализация нулями

        _joyId = FindJoystickId()
        If _joyId < 0 Then Return False

        _pollTimer = New Timer(AddressOf PollCallback, Nothing, 0, PollMs)
        _blinkTimer = New Timer(AddressOf BlinkCallback, Nothing, Timeout.Infinite, BlinkMs)
        Return True
    End Function

    ' ---------- HID: лампочки ----------

    Private Function SendCommand(bytes As Byte()) As Boolean
        SyncLock _hidLock
            If _hid = InvalidHandle Then Return False
            Return HidD_SetOutputReport(_hid, bytes, bytes.Length)
        End SyncLock
    End Function

    Public Sub SetLeds(leds As Boolean())
        Dim cmd(7) As Byte
        For i = 0 To 3
            cmd(2 + i) = If(leds(i), CByte(&HFF), CByte(0))   ' байты 3-6
        Next
        SendCommand(cmd)
    End Sub

    Private Sub SetPadLed(pad As Integer, light As Boolean)
        Dim leds(3) As Boolean
        leds(pad) = light
        SetLeds(leds)
    End Sub

    Private Function FindHidDevice(vid As Short, pid As Short) As IntPtr
        Dim hDevInfo = SetupDiGetClassDevs(GuidDevInterfaceHid, IntPtr.Zero, IntPtr.Zero,
                                           DIGCF_PRESENT Or DIGCF_DEVICEINTERFACE)
        If hDevInfo = InvalidHandle Then Return InvalidHandle
        Try
            Dim idx = 0
            While True
                Dim iface As New SP_DEVICE_INTERFACE_DATA()
                iface.cbSize = Marshal.SizeOf(GetType(SP_DEVICE_INTERFACE_DATA))
                If Not SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, GuidDevInterfaceHid, idx, iface) Then Exit While
                idx += 1

                Dim required As Integer
                SetupDiGetDeviceInterfaceDetail(hDevInfo, iface, IntPtr.Zero, 0, required, IntPtr.Zero)
                If required <= 0 Then Continue While

                Dim ptr = Marshal.AllocHGlobal(required)
                Dim path As String = Nothing
                Try
                    Marshal.WriteInt32(ptr, If(IntPtr.Size = 8, 8, 6))
                    If SetupDiGetDeviceInterfaceDetail(hDevInfo, iface, ptr, required, required, IntPtr.Zero) Then
                        path = Marshal.PtrToStringUni(New IntPtr(ptr.ToInt64() + 4))
                    End If
                Finally
                    Marshal.FreeHGlobal(ptr)
                End Try
                If path Is Nothing Then Continue While

                Dim handle = CreateFile(path, GENERIC_READ Or GENERIC_WRITE,
                                        FILE_SHARE_READ Or FILE_SHARE_WRITE,
                                        IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero)
                If handle = InvalidHandle Then Continue While

                Dim attrs As New HIDD_ATTRIBUTES()
                attrs.Size = Marshal.SizeOf(GetType(HIDD_ATTRIBUTES))
                If HidD_GetAttributes(handle, attrs) AndAlso
                   attrs.VendorID = vid AndAlso attrs.ProductID = pid Then
                    Return handle
                End If
                CloseHandle(handle)
            End While
        Finally
            SetupDiDestroyDeviceInfoList(hDevInfo)
        End Try
        Return InvalidHandle
    End Function

    ' ---------- winmm: кнопки ----------

    Private Function FindJoystickId() As Integer
        For id = 0 To joyGetNumDevs() - 1
            Dim info As New JOYINFOEX()
            info.dwSize = 52
            info.dwFlags = &HFF
            If joyGetPosEx(CUInt(id), info) = 0 Then Return id
        Next
        Return -1
    End Function

    Private Sub PollCallback(state As Object)
        If _joyId < 0 Then Return
        Dim info As New JOYINFOEX()
        info.dwSize = 52
        info.dwFlags = JOY_RETURNBUTTONS
        If joyGetPosEx(CUInt(_joyId), info) <> 0 Then Return

        Dim pressed = info.dwButtons And Not _lastButtons
        _lastButtons = info.dwButtons

        For pad = 0 To 3
            If (pressed And BuzzerMasks(pad)) <> 0 Then OnBuzzerDown(pad)
        Next
    End Sub

    ' ---------- Логика раунда ----------

    Private Sub OnBuzzerDown(pad As Integer)
        SyncLock _stateLock
            If _activePad <> -1 Then Exit Sub   ' остальные пульты заблокированы
            _activePad = pad
            _activeSince = DateTime.UtcNow
        End SyncLock

        AudioService.Instance.Play(My.Resources._26_buzzer)
        SetPadLed(pad, True)
        _blinkTimer.Change(BlinkMs, BlinkMs)
        RaiseEvent BuzzerPressed(pad)
    End Sub

    Private Sub BlinkCallback(state As Object)
        SyncLock _stateLock
            If _activePad = -1 Then Exit Sub
            If (DateTime.UtcNow - _activeSince).TotalSeconds >= ActiveWindowSec Then
                Dim pad = _activePad
                _activePad = -1
                _blinkTimer.Change(Timeout.Infinite, Timeout.Infinite)
                SetPadLed(pad, False)
            Else
                _blinkOn = Not _blinkOn
                SetPadLed(_activePad, _blinkOn)
            End If
        End SyncLock
    End Sub

    ''' <summary>Досрочно завершить раунд (например, ведущий сбрасывает вопрос)</summary>
    Public Sub CancelActive()
        SyncLock _stateLock
            If _activePad = -1 Then Exit Sub
            Dim pad = _activePad
            _activePad = -1
            _blinkTimer.Change(Timeout.Infinite, Timeout.Infinite)
            SetPadLed(pad, False)
        End SyncLock
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        _pollTimer?.Dispose()
        _blinkTimer?.Dispose()
        SendCommand(New Byte(7) {})
        If _hid <> InvalidHandle Then CloseHandle(_hid)
        _hid = InvalidHandle
    End Sub
    Public Function ListJoysticks() As String
        Dim sb As New System.Text.StringBuilder()
        For id = 0 To 15
            Dim caps As New JOYCAPS()
            Dim res = joyGetDevCaps(New IntPtr(id), caps, JOYCAPS_SIZE)
            sb.AppendLine($"ID {id}: result={res}, name='{caps.szPname}', " &
                      $"buttons={caps.wMaxButtons}, pid=&H{caps.wPid:X4}")
        Next
        Return sb.ToString()
    End Function
    Public Function Diagnose() As String
        Dim sb As New System.Text.StringBuilder()
        Dim managedSize = Marshal.SizeOf(GetType(JOYCAPS))
        sb.AppendLine($"Marshal.SizeOf(JOYCAPS) = {managedSize}")

        ' Пробуем разные размеры и явную W-функцию
        For Each size In {managedSize, 690, 366}
            Dim caps As New JOYCAPS()
            sb.AppendLine($"W(0, {size}) -> {joyGetDevCapsW(IntPtr.Zero, caps, size)} '{caps.szPname}'")
        Next

        ' Главное: работает ли вообще чтение состояния
        Dim info As New JOYINFOEX()
        info.dwSize = Marshal.SizeOf(GetType(JOYINFOEX))
        sb.AppendLine($"Marshal.SizeOf(JOYINFOEX) = {info.dwSize}")
        info.dwFlags = &HFF ' JOY_RETURNALL
        For id = 0 To 3
            Dim res = joyGetPosEx(CUInt(id), info)
            sb.AppendLine($"joyGetPosEx({id}) -> {res}, buttons={info.dwButtons}")
        Next
        Return sb.ToString()
    End Function
End Class
