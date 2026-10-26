Option Strict On
Imports System.IO
Imports System.Runtime.InteropServices
Imports HidLibrary

' Единственный владелец HID-дескриптора — фоновый цикл BuzzManager.
' Тестовый приёмник реализует тот же контракт без USB и вызовов hid.dll.
Public Interface IBuzzReceiver
    Inherits IDisposable
    Function ReadButtons() As Integer?
    Function SetLamp(pad As Integer, lit As Boolean) As Boolean
End Interface

Friend NotInheritable Class HidBuzzReceiver
    Implements IBuzzReceiver
    Private ReadOnly _device As HidDevice
    Private _preparsed As IntPtr

    Private Sub New(device As HidDevice)
        _device = device
        If Not HidD_GetPreparsedData(device.ReadHandle, _preparsed) Then Throw New IOException("Не удалось прочитать описание кнопок Buzz!.")
    End Sub

    Public Shared Function Open() As IBuzzReceiver
        Dim devices = HidDevices.Enumerate(&H54C, &H1000).ToArray()
        If devices.Length = 0 Then Return Nothing
        If devices.Length > 1 Then Throw New IOException("Найдено несколько приёмников Buzz!. Оставьте один.")
        Dim device = devices(0)
        Try
            device.OpenDevice(DeviceMode.Overlapped, DeviceMode.Overlapped, ShareMode.ShareRead Or ShareMode.ShareWrite)
            If Not device.IsOpen Then Throw New IOException("Не удалось открыть приёмник Buzz!.")
            Return New HidBuzzReceiver(device)
        Catch
            device.Dispose()
            Throw
        End Try
    End Function

    Public Function ReadButtons() As Integer? Implements IBuzzReceiver.ReadButtons
        If Not _device.IsConnected Then Throw New IOException("Приёмник Buzz! отключён.")
        Dim data = _device.Read(100)
        Select Case data.Status
            Case HidDeviceData.ReadStatus.WaitTimedOut, HidDeviceData.ReadStatus.NoDataRead
                Return Nothing ' Молчание подключённого устройства не означает отключения.
            Case HidDeviceData.ReadStatus.Success
                Dim usages(31) As UShort
                Dim count = CUInt(usages.Length)
                ' Windows разбирает фактический HID-дескриптор; не угадываем смещения байтов.
                Dim result = HidP_GetUsages(0, 9US, 0US, usages, count, _preparsed, data.Data, CUInt(data.Data.Length))
                If result <> &H110000 Then Throw New InvalidDataException("Ошибка HID-отчёта Buzz!: " & result.ToString("X8"))
                Dim mask As Integer
                For i = 0 To CInt(count) - 1
                    Dim usage = CInt(usages(i))
                    If usage >= 1 AndAlso usage <= 16 AndAlso (usage - 1) Mod 5 = 0 Then mask = mask Or (1 << ((usage - 1) \ 5))
                Next
                Return mask
            Case Else
                Throw New IOException("Потеряна связь с приёмником Buzz!.")
        End Select
    End Function

    Public Function SetLamp(pad As Integer, lit As Boolean) As Boolean Implements IBuzzReceiver.SetLamp
        Dim command(Math.Max(8, CInt(_device.Capabilities.OutputReportByteLength)) - 1) As Byte
        If pad >= 0 AndAlso pad <= 3 AndAlso lit Then command(2 + pad) = &HFF
        Return _device.Write(command, 100)
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If _preparsed <> IntPtr.Zero Then
            HidD_FreePreparsedData(_preparsed)
            _preparsed = IntPtr.Zero
        End If
        _device.Dispose()
    End Sub

    <DllImport("hid.dll", SetLastError:=True)>
    Private Shared Function HidD_GetPreparsedData(handle As IntPtr, ByRef data As IntPtr) As <MarshalAs(UnmanagedType.U1)> Boolean
    End Function
    <DllImport("hid.dll")>
    Private Shared Function HidD_FreePreparsedData(data As IntPtr) As <MarshalAs(UnmanagedType.U1)> Boolean
    End Function
    <DllImport("hid.dll")>
    Private Shared Function HidP_GetUsages(reportType As Integer, usagePage As UShort, linkCollection As UShort, <Out> usages As UShort(), ByRef count As UInteger, preparsed As IntPtr, report As Byte(), reportLength As UInteger) As Integer
    End Function
End Class
