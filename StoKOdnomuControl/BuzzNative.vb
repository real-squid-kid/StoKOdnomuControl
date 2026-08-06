Imports System.Runtime.InteropServices

Friend Module BuzzNative
    <StructLayout(LayoutKind.Sequential)>
    Friend Structure HIDD_ATTRIBUTES
        Friend Size As Integer
        Friend VendorID As Short
        Friend ProductID As Short
        Friend VersionNumber As Short
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Friend Structure SP_DEVICE_INTERFACE_DATA
        Friend cbSize As Integer
        Friend InterfaceClassGuid As Guid
        Friend Flags As Integer
        Friend Reserved As IntPtr
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Friend Structure JOYINFOEX
        Friend dwSize As Integer
        Friend dwFlags As Integer
        Friend dwXpos As Integer
        Friend dwYpos As Integer
        Friend dwZpos As Integer
        Friend dwRpos As Integer
        Friend dwUpos As Integer
        Friend dwVpos As Integer
        Friend dwButtons As Integer
        Friend dwButtonNumber As Integer
        Friend dwPOV As Integer
        Friend dwReserved1 As Integer
        Friend dwReserved2 As Integer
    End Structure
    ' Размер JOYINFOEX = 64 байта (фиксировано)
    Friend Const JOYINFOEX_SIZE As Integer = 52

    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Auto)>
    Friend Structure JOYCAPS
        Friend wMid As Short
        Friend wPid As Short
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=32)> Friend szPname As String
        Friend wXmin As Short
        Friend wXmax As Short
        Friend wYmin As Short
        Friend wYmax As Short
        Friend wZmin As Short
        Friend wZmax As Short
        Friend wNumButtons As Short
        Friend wPeriodMin As Short
        Friend wPeriodMax As Short
        Friend wRmin As Short
        Friend wRmax As Short
        Friend wUmin As Short
        Friend wUmax As Short
        Friend wVmin As Short
        Friend wVmax As Short
        Friend wCaps As Short
        Friend wMaxAxes As Short
        Friend wNumAxes As Short
        Friend wMaxButtons As Short
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=32)> Friend szRegKey As String
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=260)> Friend szOEMVxD As String
    End Structure

    ' Размер JOYCAPS = 512 байт (фиксировано для Unicode)
    Friend Const JOYCAPS_SIZE As Integer = 690

    Friend ReadOnly GuidDevInterfaceHid As New Guid("4D1E55B2-F16F-11CF-88CB-001111000000")

    Friend Const DIGCF_PRESENT As Integer = 2
    Friend Const DIGCF_DEVICEINTERFACE As Integer = &H10
    Friend Const GENERIC_READ As Integer = Integer.MinValue
    Friend Const GENERIC_WRITE As Integer = &H40000000
    Friend Const FILE_SHARE_READ As Integer = 1
    Friend Const FILE_SHARE_WRITE As Integer = 2
    Friend Const OPEN_EXISTING As Integer = 3
    Friend Const JOY_RETURNBUTTONS As Integer = &H80
    Friend ReadOnly InvalidHandle As New IntPtr(-1)

    <DllImport("setupapi.dll", SetLastError:=True)>
    Friend Function SetupDiGetClassDevs(ByRef g As Guid, ByVal enumerator As IntPtr,
        ByVal hwnd As IntPtr, ByVal flags As Integer) As IntPtr
    End Function

    <DllImport("setupapi.dll", SetLastError:=True)>
    Friend Function SetupDiEnumDeviceInterfaces(ByVal hDevInfo As IntPtr, ByVal devInfo As IntPtr,
        ByRef g As Guid, ByVal index As Integer, ByRef data As SP_DEVICE_INTERFACE_DATA) As Boolean
    End Function

    <DllImport("setupapi.dll", SetLastError:=True)>
    Friend Function SetupDiGetDeviceInterfaceDetail(ByVal hDevInfo As IntPtr,
        ByRef data As SP_DEVICE_INTERFACE_DATA, ByVal detail As IntPtr,
        ByVal detailSize As Integer, ByRef requiredSize As Integer,
        ByVal devInfoData As IntPtr) As Boolean
    End Function

    <DllImport("setupapi.dll")>
    Friend Function SetupDiDestroyDeviceInfoList(ByVal hDevInfo As IntPtr) As Boolean
    End Function

    <DllImport("kernel32.dll", SetLastError:=True, CharSet:=CharSet.Auto)>
    Friend Function CreateFile(ByVal name As String, ByVal access As Integer,
        ByVal share As Integer, ByVal sec As IntPtr, ByVal creation As Integer,
        ByVal flags As Integer, ByVal template As IntPtr) As IntPtr
    End Function

    <DllImport("kernel32.dll")>
    Friend Function CloseHandle(ByVal handle As IntPtr) As Boolean
    End Function

    <DllImport("hid.dll")>
    Friend Function HidD_GetAttributes(ByVal handle As IntPtr, ByRef attrs As HIDD_ATTRIBUTES) As Boolean
    End Function

    <DllImport("hid.dll")>
    Friend Function HidD_SetOutputReport(ByVal handle As IntPtr,
        ByVal buffer As Byte(), ByVal length As Integer) As Boolean
    End Function

    <DllImport("winmm.dll")>
    Friend Function joyGetNumDevs() As Integer
    End Function

    <DllImport("winmm.dll", CharSet:=CharSet.Auto)>
    Friend Function joyGetDevCaps(ByVal id As IntPtr, ByRef caps As JOYCAPS, ByVal size As Integer) As Integer
    End Function

    <DllImport("winmm.dll")>
    Friend Function joyGetPosEx(ByVal id As UInteger, ByRef info As JOYINFOEX) As Integer
    End Function
    <DllImport("winmm.dll", EntryPoint:="joyGetDevCapsW")>
    Friend Function joyGetDevCapsW(ByVal id As IntPtr, ByRef caps As JOYCAPS, ByVal size As Integer) As Integer
    End Function
End Module