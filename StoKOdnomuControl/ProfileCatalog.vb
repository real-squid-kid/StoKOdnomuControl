Option Strict On
Imports System.IO

Public NotInheritable Class ProfileChoice
    Public Property Name As String
    Public Property ConfigFile As String = ""
    Public Overrides Function ToString() As String
        Return Name
    End Function
End Class

Public Module ProfileCatalog
    Public Function Discover(root As String, errors As List(Of String)) As List(Of ProfileChoice)
        Dim choices As New List(Of ProfileChoice)()
        If Not Directory.Exists(root) Then Return choices
        For Each folder In Directory.GetDirectories(root).OrderBy(Function(p) p, StringComparer.OrdinalIgnoreCase)
            Dim config = AppearanceStorage.ConfigPath(folder)
            If Not File.Exists(config) Then Continue For
            Try
                Dim profile = AppearanceStorage.Open(config)
                choices.Add(New ProfileChoice With {.Name = profile.Name, .ConfigFile = config})
            Catch ex As Exception
                ' Один испорченный пользовательский профиль не скрывает остальные.
                errors.Add(IO.Path.GetFileName(folder) & ": " & ex.Message)
            End Try
        Next
        Return choices
    End Function
End Module
