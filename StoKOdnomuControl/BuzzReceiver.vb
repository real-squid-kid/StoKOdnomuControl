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
    Private ReadOnly _inputLength As Integer
    Private ReadOnly _outputLength As Integer

    Private Sub New(device As HidDevice)
        _device = device
        _inputLength = CInt(device.Capabilities.InputReportByteLength)
        _outputLength = CInt(device.Capabilities.OutputReportByteLength)
        If _inputLength <= 0 Then Throw New IOException("Приёмник не описывает входные отчёты Buzz!.")
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
        ' HidLibrary.Read/Write с таймаутом не завершают отменённый OVERLAPPED
        ' перед освобождением памяти. Поздний USB-отчёт мог повредить память процесса.
        ' Библиотека используется только для поиска/открытия и описания устройства.
        Dim data(_inputLength - 1) As Byte
        Dim received = OverlappedIo.Transfer(_device.ReadHandle, data, False, 100)
        If Not received.HasValue OrElse received.Value = 0 Then Return Nothing
        If received.Value <> data.Length Then Throw New InvalidDataException("Неполный HID-отчёт Buzz!.")
        Dim usages(31) As UShort
        Dim count = CUInt(usages.Length)
        ' Windows разбирает фактический HID-дескриптор; не угадываем смещения байтов.
        Dim result = HidP_GetUsages(0, 9US, 0US, usages, count, _preparsed, data, CUInt(data.Length))
        If result <> &H110000 Then Throw New InvalidDataException("Ошибка HID-отчёта Buzz!: " & result.ToString("X8"))
        If count > CUInt(usages.Length) Then Throw New InvalidDataException("Слишком много кнопок в HID-отчёте Buzz!.")
        Dim mask As Integer
        For i = 0 To CInt(count) - 1
            Dim usage = CInt(usages(i))
            If usage >= 1 AndAlso usage <= 16 AndAlso (usage - 1) Mod 5 = 0 Then mask = mask Or (1 << ((usage - 1) \ 5))
        Next
        Return mask
    End Function

    Public Function SetLamp(pad As Integer, lit As Boolean) As Boolean Implements IBuzzReceiver.SetLamp
        If _outputLength < 6 Then Return False
        Dim command(_outputLength - 1) As Byte
        If pad >= 0 AndAlso pad <= 3 AndAlso lit Then command(2 + pad) = &HFF
        Dim written = OverlappedIo.Transfer(_device.WriteHandle, command, True, 100)
        Return written.HasValue AndAlso written.Value = command.Length
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
