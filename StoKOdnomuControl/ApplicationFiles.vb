Option Strict On
Imports System.IO

' Все комплектные файлы ищутся относительно EXE, независимо от текущей папки
' процесса или последнего файлового диалога. Пользовательские сохранения отдельно.
Public Module ApplicationFiles
    Public ReadOnly Property LibraryDirectory As String
        Get
            Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib")
        End Get
    End Property
    Public ReadOnly Property FontsDirectory As String
        Get
            Return Path.Combine(LibraryDirectory, "Fonts")
        End Get
    End Property
    Public ReadOnly Property ProfilesDirectory As String
        Get
            Return Path.Combine(LibraryDirectory, "profiles")
        End Get
    End Property
    Public ReadOnly Property BanksDirectory As String
        Get
            Return Path.Combine(LibraryDirectory, "bases")
        End Get
    End Property
End Module
