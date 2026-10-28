Option Strict On
Imports System.IO

' Один протокол записи для матча, TXT и профилей: подготовить и сбросить данные
' в файл того же каталога, затем атомарно опубликовать. Формат определяет вызывающий.
Public Module AtomicFile
    Public Sub Write(path As String, bytes As Byte())
        Write(path, Sub(stream) stream.Write(bytes, 0, bytes.Length),
              verify:=Sub(temporary)
                          If Not bytes.SequenceEqual(File.ReadAllBytes(temporary)) Then Throw New IOException("Не удалось проверить записанный файл.")
                      End Sub)
    End Sub

    Public Sub Write(path As String, content As Action(Of Stream), Optional backupPath As String = Nothing, Optional verify As Action(Of String) = Nothing)
        Dim target = IO.Path.GetFullPath(path)
        Dim temporary = target & "." & Guid.NewGuid().ToString("N") & ".tmp"
        Try
            Using stream As New FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)
                content(stream)
                stream.Flush(True)
            End Using
            If verify IsNot Nothing Then verify(temporary)
            If File.Exists(target) Then
                Replace(temporary, target, If(backupPath, target & ".bak"))
            Else
                File.Move(temporary, target)
            End If
        Finally
            ' Не маскируем первоначальную ошибку ошибкой удаления временного файла.
            ' Остаток после аварии не участвует ни в чтении, ни в восстановлении.
            If File.Exists(temporary) Then
                Try
                    File.Delete(temporary)
                Catch ex As Exception When TypeOf ex Is IOException OrElse TypeOf ex Is UnauthorizedAccessException
                    Diagnostics.Debug.WriteLine(ex.Message)
                End Try
            End If
        End Try
    End Sub

    Private Sub Replace(temporary As String, target As String, backup As String)
        For attempt = 1 To 4
            Try
                File.Replace(temporary, target, backup, True)
                Return
            Catch ex As IOException When attempt < 4 AndAlso (ex.HResult And &HFFFF) = 32
                ' Только короткая блокировка совместного доступа заслуживает повтора;
                ' ошибки прав/диска сразу возвращаются вызывающему окну.
                Threading.Thread.Sleep(20)
            End Try
        Next
    End Sub

    Public Function RejectedPath(target As String) As String
        Return target & ".rejected-" & DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") & "-" & Guid.NewGuid().ToString("N")
    End Function
End Module
