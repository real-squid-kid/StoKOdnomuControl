Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports HidLibrary

Public Class BuzzManager
    Public Event BuzzerPressed(ByVal pad As Integer)

    Private Const Vid As Integer = &H54C
    Private Const Pid As Integer = &H1000

    Private _device As HidDevice
    Private _joyId As Integer = -1
    Private _lastButtons As Integer
    Private _activePad As Integer = -1
    Private _blinkOn As Boolean
    Private _blinkTicks As Integer

    Private WithEvents _pollTimer As New Timer With {.Interval = 25}
    Private WithEvents _blinkTimer As New Timer With {.Interval = 250}

    ' ---------- Инициализация ----------

    Public Function Initialize() As Boolean
        _device = HidDevices.Enumerate(Vid, Pid).FirstOrDefault()
        If _device Is Nothing Then Return False
        _device.OpenDevice()
        SendLeds(New Boolean() {False, False, False, False})   ' те самые нули

        For id = 0 To 15
            If ReadButtons(id) >= 0 Then
                _joyId = id
                Exit For
            End If
        Next
        If _joyId < 0 Then Return False

        _pollTimer.Start()
        Return True
    End Function

    Public Sub Shutdown()
        _pollTimer.Stop()
        _blinkTimer.Stop()
        If _device IsNot Nothing Then
            SendLeds(New Boolean() {False, False, False, False})
            _device.Dispose()
            _device = Nothing
        End If
    End Sub

    ' ---------- Лампочки ----------

    Public Sub SetPadLed(ByVal pad As Integer, ByVal light As Boolean)
        Dim leds(3) As Boolean
        leds(pad) = light
        SendLeds(leds)
    End Sub

    Private Sub SendLeds(ByVal leds As Boolean())
        If _device Is Nothing Then Exit Sub
        Dim cmd(7) As Byte
        For i = 0 To 3
            cmd(2 + i) = If(leds(i), CByte(&HFF), CByte(0))
        Next
        _device.Write(cmd)   ' если в вашей версии HidLibrary нет WriteReport — _device.Write(cmd)
    End Sub

    ' ---------- Кнопки ----------

    Private Function ReadButtons(ByVal id As Integer) As Integer
        Dim info As New JOYINFOEX()
        info.dwSize = 52
        info.dwFlags = &HFF
        If joyGetPosEx(CUInt(id), info) = 0 Then Return info.dwButtons
        Return -1
    End Function

    Private Sub PollTimer_Tick() Handles _pollTimer.Tick
        Dim cur = ReadButtons(_joyId)
        If cur < 0 Then Return

        Dim pressed = cur And Not _lastButtons
        _lastButtons = cur

        For pad = 0 To 3
            ' Кнопки 1, 6, 11, 16 -> биты 0, 5, 10, 15
            If (pressed And (1 << (pad * 5))) <> 0 Then OnPress(pad)
        Next
    End Sub

    ' ---------- Логика раунда ----------

    Private Sub OnPress(ByVal pad As Integer)
        If _activePad >= 0 Then Exit Sub          ' остальные заблокированы
        _activePad = pad
        _blinkTicks = 30                           ' 30 * 500 мс = 15 секунд

        AudioService.Instance.Play(My.Resources._26_buzzer)
        SetPadLed(pad, True)
        _blinkTimer.Start()
        RaiseEvent BuzzerPressed(pad)
    End Sub

    Private Sub BlinkTimer_Tick() Handles _blinkTimer.Tick
        _blinkTicks -= 1
        _blinkOn = Not _blinkOn

        If _blinkTicks <= 0 Then
            _blinkTimer.Stop()
            SetPadLed(_activePad, False)
            _activePad = -1                        ' разблокировали пульты
        Else
            SetPadLed(_activePad, _blinkOn)
        End If
    End Sub

    ''' <summary>Досрочный сброс раунда</summary>
    Public Sub CancelActive()
        If _activePad < 0 Then Exit Sub
        _blinkTimer.Stop()
        SetPadLed(_activePad, False)
        _activePad = -1
    End Sub

    ' ---------- winmm (единственный P/Invoke) ----------

    <StructLayout(LayoutKind.Sequential)>
    Private Structure JOYINFOEX
        Public dwSize As Integer
        Public dwFlags As Integer
        Public dwXpos As Integer, dwYpos As Integer, dwZpos As Integer
        Public dwRpos As Integer, dwUpos As Integer, dwVpos As Integer
        Public dwButtons As Integer
        Public dwButtonNumber As Integer
        Public dwPOV As Integer
        Public dwReserved1 As Integer
        Public dwReserved2 As Integer
    End Structure

    <DllImport("winmm.dll")>
    Private Shared Function joyGetPosEx(ByVal id As UInteger, ByRef info As JOYINFOEX) As Integer
    End Function
End Class
