Option Strict On
Imports System.Diagnostics
Imports System.IO
Imports System.IO.Pipes
Imports System.Threading
Imports System.Threading.Tasks
Imports StoKOdnomuControl

' Настоящее Windows overlapped I/O на именованных каналах, без доступа к USB.
' Подставной IBuzzReceiver не проверяет время жизни нативных буферов и отмену.
Friend Module UsbIoChecks
    Public Sub Run(check As Action(Of Boolean, String))
        ' Прогреваем инфраструктуру каналов до сравнения числа дескрипторов.
        Using pair As New PipePair()
            check(Not OverlappedIo.Transfer(pair.ServerHandle, New Byte(31) {}, False, 1).HasValue, "idle I/O cancels and drains")
        End Using
        Dim before, after As Integer
        Using pair As New PipePair(), currentProcess = Process.GetCurrentProcess()
            before = currentProcess.HandleCount
            For iteration = 1 To 120
                check(Not OverlappedIo.Transfer(pair.ServerHandle, New Byte(31) {}, False, 1).HasValue, "repeated timeout completes before free")
                If iteration Mod 10 = 0 Then GC.Collect()
            Next
            after = currentProcess.HandleCount
            check(after <= before + 2, "idle cancellation does not leak event handles: " & before & " -> " & after)
            ' Следующий пакет обязан попасть в НОВЫЙ запрос, а не в прежний,
            ' уже освобождённый буфер отменённого чтения.
            For iteration = 1 To 32
                Dim sequence = iteration
                Dim payload = Enumerable.Range(0, 32).Select(Function(value) CByte(value + sequence)).ToArray()
                Dim target(31) As Byte
                Using read = Task.Run(Function() OverlappedIo.Transfer(pair.ServerHandle, target, False, 2000))
                    Thread.Sleep(2)
                    Dim written = OverlappedIo.Transfer(pair.ClientHandle, payload, True, 2000)
                    check(read.Wait(3000), "pending read finishes after packet")
                    check(written.HasValue AndAlso written.Value = payload.Length AndAlso read.Result.HasValue AndAlso read.Result.Value = payload.Length AndAlso target.SequenceEqual(payload), "packet after cancellation is intact")
                End Using
            Next
            Dim buffered = New Byte() {4, 3, 2, 1}
            Dim received(3) As Byte
            check(OverlappedIo.Transfer(pair.ClientHandle, buffered, True, 2000).Value = 4, "buffered packet write")
            check(OverlappedIo.Transfer(pair.ServerHandle, received, False, 2000).Value = 4 AndAlso received.SequenceEqual(buffered), "immediate read completion")
            pair.Client.Dispose()
            Dim failed As Boolean
            Try
                OverlappedIo.Transfer(pair.ServerHandle, New Byte(31) {}, False, 100)
            Catch ex As IOException
                failed = True
            End Try
            check(failed, "disconnect is an I/O error, not an idle timeout")
        End Using
        ' Запись, которую никто не читает, должна также отмениться и завершиться
        ' до освобождения большого буфера (отдельный канал не содержит старых данных).
        For iteration = 1 To 12
            Using pair As New PipePair()
                check(Not OverlappedIo.Transfer(pair.ClientHandle, New Byte(1048575) {}, True, 1).HasValue, "blocked write cancels and drains")
            End Using
        Next
        Dim invalidFailed As Boolean
        Try
            OverlappedIo.Transfer(New IntPtr(-1), New Byte(31) {}, False, 1)
        Catch ex As IOException
            invalidFailed = True
        End Try
        check(invalidFailed, "invalid handle reports an I/O error")
        Console.WriteLine("Windows overlapped I/O: timeout, late packet, write cancellation, disconnect PASS; handles " & before & " -> " & after)
    End Sub

    Private NotInheritable Class PipePair
        Implements IDisposable
        Public ReadOnly Server As NamedPipeServerStream
        Public ReadOnly Client As NamedPipeClientStream

        Public Sub New()
            Dim name = "StoKOdnomu-io-" & Guid.NewGuid().ToString("N")
            Server = New NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 1024, 1024)
            Client = New NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous)
            Try
                Using connect = Server.WaitForConnectionAsync()
                    Client.Connect(1000)
                    If Not connect.Wait(2000) Then Throw New TimeoutException("Test pipe connection timed out.")
                End Using
            Catch
                Dispose()
                Throw
            End Try
        End Sub

        Public ReadOnly Property ServerHandle As IntPtr
            Get
                Return Server.SafePipeHandle.DangerousGetHandle()
            End Get
        End Property
        Public ReadOnly Property ClientHandle As IntPtr
            Get
                Return Client.SafePipeHandle.DangerousGetHandle()
            End Get
        End Property

        Public Sub Dispose() Implements IDisposable.Dispose
            Client.Dispose()
            Server.Dispose()
        End Sub
    End Class
End Module
