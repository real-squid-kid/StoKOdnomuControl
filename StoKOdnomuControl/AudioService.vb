Option Strict On
Imports System.Collections.Concurrent
Imports System.IO
Imports NAudio.Wave
Imports NAudio.Wave.SampleProviders

Public Enum SoundCue
    OpenAnswer
    NoScore
    WrongAnswer
    BigWrong
    SameAnswer
    Transfer
    Victory
    Buzzer
    Countdown20
    Countdown25
    Reveal1
    Reveal2
    Reveal3
    Reveal4
    Reveal5
    EasterEgg
End Enum

Public Enum AudioChannel
    Effect
    Countdown
    Buzzer
    EasterEgg
End Enum

' Decode each resource once; playback owns only a cursor into immutable PCM samples.
' Readers are disposed during decoding, not on the audio callback thread.
Public NotInheritable Class AudioService
    Implements IDisposable
    Private Shared ReadOnly _instance As New AudioService()
    Private ReadOnly _gate As New Object()
    Private ReadOnly _cache As New Dictionary(Of SoundCue, Single())()
    Private ReadOnly _active As New ConcurrentDictionary(Of Guid, CachedSoundProvider)()
    Private ReadOnly _mixer As New MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2)) With {.ReadFully = True}
    Private _output As WaveOutEvent
    Private _disposed As Boolean
    Public Property Enabled As Boolean = True
    Public Event AudioError As Action(Of String)

    Private Sub New()
        AddHandler _mixer.MixerInputEnded, Sub(sender, e)
                                              Dim sound = TryCast(e.SampleProvider, CachedSoundProvider)
                                              If sound IsNot Nothing Then
                                                  Dim removed As CachedSoundProvider = Nothing
                                                  _active.TryRemove(sound.Id, removed)
                                              End If
                                          End Sub
    End Sub

    Public Shared ReadOnly Property Instance As AudioService
        Get
            Return _instance
        End Get
    End Property

    Public Function Play(cue As SoundCue, Optional channel As AudioChannel = AudioChannel.Effect) As Guid
        Try
            SyncLock _gate
                If _disposed OrElse Not Enabled Then Return Guid.Empty
                If Not _cache.ContainsKey(cue) Then _cache.Add(cue, Decode(cue))
                If _output Is Nothing Then
                    Dim output As New WaveOutEvent()
                    Try
                        output.Init(_mixer)
                        AddHandler output.PlaybackStopped, AddressOf OnPlaybackStopped
                        output.Play()
                        _output = output
                    Catch
                        output.Dispose()
                        Throw
                    End Try
                End If
                StopChannel(channel)
                Dim sound As New CachedSoundProvider(_cache(cue), channel, _mixer.WaveFormat)
                _active.TryAdd(sound.Id, sound)
                _mixer.AddMixerInput(sound)
                If _output.PlaybackState <> PlaybackState.Playing Then _output.Play()
                Return sound.Id
            End SyncLock
        Catch ex As Exception
            RaiseEvent AudioError("Звук недоступен: " & ex.Message)
            Return Guid.Empty
        End Try
    End Function

    Public Sub [Stop](id As Guid)
        SyncLock _gate
            Dim sound As CachedSoundProvider = Nothing
            If _active.TryRemove(id, sound) Then _mixer.RemoveMixerInput(sound)
        End SyncLock
    End Sub

    Public Sub StopChannel(channel As AudioChannel)
        SyncLock _gate
            For Each pair In _active.ToArray()
                If pair.Value.Channel = channel Then [Stop](pair.Key)
            Next
        End SyncLock
    End Sub

    Public Sub StopAll()
        SyncLock _gate
            _mixer.RemoveAllMixerInputs()
            _active.Clear()
        End SyncLock
    End Sub

    Private Sub OnPlaybackStopped(sender As Object, e As StoppedEventArgs)
        If e.Exception IsNot Nothing Then RaiseEvent AudioError("Ошибка аудиоустройства: " & e.Exception.Message)
    End Sub

    Private Shared Function OpenResource(cue As SoundCue) As Stream
        Select Case cue
            Case SoundCue.OpenAnswer : Return My.Resources._26_open
            Case SoundCue.NoScore : Return My.Resources._26_noscore
            Case SoundCue.WrongAnswer : Return My.Resources._26_wrong
            Case SoundCue.BigWrong : Return My.Resources._26_bigwrong
            Case SoundCue.SameAnswer : Return My.Resources._26_same
            Case SoundCue.Transfer : Return My.Resources.bing
            Case SoundCue.Victory : Return My.Resources._26_win
            Case SoundCue.Buzzer : Return My.Resources._26_buzzer
            Case SoundCue.Countdown20 : Return My.Resources._20sec
            Case SoundCue.Countdown25 : Return My.Resources._25sec
            Case SoundCue.Reveal1 : Return My.Resources.ff_reveal1
            Case SoundCue.Reveal2 : Return My.Resources.ff_reveal2
            Case SoundCue.Reveal3 : Return My.Resources.ff_reveal3
            Case SoundCue.Reveal4 : Return My.Resources.ff_reveal4
            Case SoundCue.Reveal5 : Return My.Resources.ff_reveal5
            Case SoundCue.EasterEgg : Return My.Resources.run
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(cue))
        End Select
    End Function

    Private Function Decode(cue As SoundCue) As Single()
        Using stream = OpenResource(cue), reader As New WaveFileReader(stream)
            Dim provider = reader.ToSampleProvider()
            If provider.WaveFormat.Channels = 1 Then provider = New MonoToStereoSampleProvider(provider)
            If provider.WaveFormat.Channels <> 2 Then Throw New InvalidDataException("Ожидается моно или стерео WAV.")
            If provider.WaveFormat.SampleRate <> 48000 Then provider = New WdlResamplingSampleProvider(provider, 48000)
            Dim samples As New List(Of Single)()
            Dim buffer(8191) As Single
            Do
                Dim count = provider.Read(buffer, 0, buffer.Length)
                If count = 0 Then Exit Do
                For i = 0 To count - 1
                    samples.Add(buffer(i))
                Next
            Loop
            Return samples.ToArray()
        End Using
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        Dim output As WaveOutEvent
        SyncLock _gate
            If _disposed Then Return
            _disposed = True
            output = _output
            _output = Nothing
            StopAll()
            _cache.Clear()
        End SyncLock
        If output IsNot Nothing Then
            RemoveHandler output.PlaybackStopped, AddressOf OnPlaybackStopped
            output.Stop()
            output.Dispose()
        End If
    End Sub

    Private NotInheritable Class CachedSoundProvider
        Implements ISampleProvider
        Public ReadOnly Id As Guid = Guid.NewGuid()
        Public ReadOnly Channel As AudioChannel
        Private ReadOnly _samples As Single()
        Private _position As Integer
        Public ReadOnly Property WaveFormat As WaveFormat Implements ISampleProvider.WaveFormat

        Public Sub New(samples As Single(), channel As AudioChannel, format As WaveFormat)
            _samples = samples
            Me.Channel = channel
            WaveFormat = format
        End Sub

        Public Function Read(buffer As Single(), offset As Integer, count As Integer) As Integer Implements ISampleProvider.Read
            Dim available = Math.Min(count, _samples.Length - _position)
            Array.Copy(_samples, _position, buffer, offset, available)
            _position += available
            Return available
        End Function
    End Class
End Class
