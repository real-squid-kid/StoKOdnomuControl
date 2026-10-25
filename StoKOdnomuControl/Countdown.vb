Option Strict On
Imports System.Diagnostics

' Stopwatch measures time; the UI timer only requests a repaint.
Public NotInheritable Class Countdown
    Implements IDisposable
    Private ReadOnly _watch As New Stopwatch()
    Private ReadOnly _timer As New Windows.Forms.Timer With {.Interval = 50}
    Private _duration As Integer
    Private _sound As Guid
    Public Property Visible As Boolean
        Get
            Return _visible
        End Get
        Private Set(value As Boolean)
            _visible = value
        End Set
    End Property
    Private _visible As Boolean
    Public Event Changed As EventHandler

    Public Sub New()
        AddHandler _timer.Tick, AddressOf OnTick
    End Sub

    Public ReadOnly Property SecondsRemaining As Integer
        Get
            Return Math.Max(0, CInt(Math.Ceiling(_duration - _watch.Elapsed.TotalSeconds)))
        End Get
    End Property

    Public ReadOnly Property Running As Boolean
        Get
            Return _watch.IsRunning
        End Get
    End Property

    Public Sub Start(seconds As Integer)
        If seconds <> 20 AndAlso seconds <> 25 Then Throw New ArgumentOutOfRangeException(NameOf(seconds))
        [Stop]()
        _duration = seconds
        Visible = True
        _sound = AudioService.Instance.Play(If(seconds = 20, SoundCue.Countdown20, SoundCue.Countdown25), AudioChannel.Countdown)
        _watch.Restart()
        _timer.Start()
        RaiseEvent Changed(Me, EventArgs.Empty)
    End Sub

    Public Sub [Stop]()
        _timer.Stop()
        _watch.Reset()
        _duration = 0
        Visible = False
        AudioService.Instance.Stop(_sound)
        _sound = Guid.Empty
        RaiseEvent Changed(Me, EventArgs.Empty)
    End Sub

    Private Sub OnTick(sender As Object, e As EventArgs)
        If SecondsRemaining = 0 Then
            _timer.Stop()
            _watch.Stop()
            ' The recording's short ending is allowed to finish naturally at zero.
        End If
        RaiseEvent Changed(Me, EventArgs.Empty)
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        [Stop]()
        _timer.Dispose()
    End Sub
End Class
