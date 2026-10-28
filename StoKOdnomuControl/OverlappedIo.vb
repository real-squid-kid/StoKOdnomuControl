Option Strict On
Imports System.ComponentModel
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Threading

' Один запрос принадлежит вызывающему потоку до фактического завершения Windows I/O.
' Таймаут ожидания сам по себе НЕ отменяет запрос: ядро ещё может писать в буфер
' и OVERLAPPED. Освобождать их можно только после CancelIoEx + GetOverlappedResult.
Friend NotInheritable Class OverlappedIo
    Private Const ErrorIoPending As Integer = 997
    Private Const ErrorOperationAborted As Integer = 995

    Private Sub New()
    End Sub

    ' Nothing означает отменённое по таймауту ожидание; ошибка устройства — IOException.
    ' handle открыт с FILE_FLAG_OVERLAPPED. Владелец не закрывает его параллельно.
    Friend Shared Function Transfer(handle As IntPtr, buffer As Byte(), write As Boolean, timeoutMilliseconds As Integer) As Integer?
        If buffer Is Nothing OrElse buffer.Length = 0 Then Throw New ArgumentException("Пустой буфер I/O.", NameOf(buffer))
        If timeoutMilliseconds < 0 Then Throw New ArgumentOutOfRangeException(NameOf(timeoutMilliseconds))
        Dim data = IntPtr.Zero
        Dim request = IntPtr.Zero
        Dim pending As Boolean
        Using completed As New ManualResetEvent(False)
            Try
                data = Marshal.AllocHGlobal(buffer.Length)
                request = Marshal.AllocHGlobal(Marshal.SizeOf(GetType(NativeOverlapped)))
                Dim native As New NativeOverlapped With {.EventHandle = completed.SafeWaitHandle.DangerousGetHandle()}
                Marshal.StructureToPtr(native, request, False)
                If write Then Marshal.Copy(buffer, 0, data, buffer.Length)
                Dim immediate = If(write, WriteFile(handle, data, CUInt(buffer.Length), IntPtr.Zero, request), ReadFile(handle, data, CUInt(buffer.Length), IntPtr.Zero, request))
                If Not immediate Then
                    Dim failure = Marshal.GetLastWin32Error()
                    If failure <> ErrorIoPending Then Throw IoError(failure)
                    pending = True
                End If

                Dim expired = pending AndAlso Not completed.WaitOne(timeoutMilliseconds)
                If expired Then CancelIoEx(handle, request)
                ' Даже если отмена опоздала и запрос уже завершился, получаем реальный
                ' результат. При ERROR_NOT_FOUND от CancelIoEx всё равно ждём завершения.
                Dim count As UInteger
                Dim success = GetOverlappedResult(handle, request, count, True)
                Dim errorCode = If(success, 0, Marshal.GetLastWin32Error())
                pending = False
                If Not success Then
                    If expired AndAlso errorCode = ErrorOperationAborted Then Return Nothing
                    Throw IoError(errorCode)
                End If
                If count > CUInt(buffer.Length) Then Throw New IOException("Некорректная длина ответа USB.")
                If Not write AndAlso count > 0 Then Marshal.Copy(data, buffer, 0, CInt(count))
                Return CInt(count)
            Finally
                ' В том числе исключение во время ожидания не оставляет ядру указатели
                ' на освобождённую память. Ожидание выполняется только в USB-worker.
                If pending Then
                    CancelIoEx(handle, request)
                    Dim ignored As UInteger
                    GetOverlappedResult(handle, request, ignored, True)
                End If
                If request <> IntPtr.Zero Then Marshal.FreeHGlobal(request)
                If data <> IntPtr.Zero Then Marshal.FreeHGlobal(data)
            End Try
        End Using
    End Function

    Private Shared Function IoError(code As Integer) As IOException
        Return New IOException("Ошибка обмена с USB: " & New Win32Exception(code).Message & " (" & code & ").", New Win32Exception(code))
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function ReadFile(handle As IntPtr, buffer As IntPtr, length As UInteger, bytesRead As IntPtr, overlapped As IntPtr) As Boolean
    End Function
    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function WriteFile(handle As IntPtr, buffer As IntPtr, length As UInteger, bytesWritten As IntPtr, overlapped As IntPtr) As Boolean
    End Function
    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function CancelIoEx(handle As IntPtr, overlapped As IntPtr) As Boolean
    End Function
    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function GetOverlappedResult(handle As IntPtr, overlapped As IntPtr, ByRef count As UInteger, wait As Boolean) As Boolean
    End Function
End Class
