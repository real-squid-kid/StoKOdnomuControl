Imports System.Threading
Imports NAudio.Wave
Imports NAudio.Wave.SampleProviders
Imports System.IO
Public Class AudioService
    Private Shared _instance As AudioService
    Private Shared ReadOnly _lock As New Object()

    Private ReadOnly _mixer As MixingSampleProvider
    Private ReadOnly _waveOut As WaveOutEvent
    Private ReadOnly _mixerLock As New Object()
    Private ReadOnly _activeInputs As New List(Of ISampleProvider)()

    Private Sub New()
        _mixer = New MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))
        _mixer.ReadFully = True
        _waveOut = New WaveOutEvent()
        _waveOut.Init(_mixer)
        _waveOut.Play()
    End Sub

    Public Shared ReadOnly Property Instance As AudioService
        Get
            If _instance Is Nothing Then
                SyncLock _lock
                    If _instance Is Nothing Then
                        _instance = New AudioService()
                    End If
                End SyncLock
            End If
            Return _instance
        End Get
    End Property

    ' Воспроизвести звук поверх текущего.
    ' Короткие SFX диспозятся автоматически после проигрывания.
    Public Sub Play(filePath As String, Optional volume As Single = 1.0F)
        SyncLock _mixerLock
            Dim reader As New AudioFileReader(filePath)
            _mixer.AddMixerInput(PrepareSampleProvider(reader, volume))
        End SyncLock
    End Sub
    Public Sub Play(resourceData As Byte(), Optional volume As Single = 1.0F)
        SyncLock _mixerLock
            Dim ms As New MemoryStream(resourceData)
            Dim reader As New WaveFileReader(ms)
            _mixer.AddMixerInput(PrepareSampleProvider(reader.ToSampleProvider(), volume))
        End SyncLock
    End Sub


    ' Перегрузка для UnmanagedMemoryStream (некоторые ресурсы возвращают его)
    Public Sub Play(resourceStream As Stream, Optional volume As Single = 1.0F)
        SyncLock _mixerLock
            Dim ms As New MemoryStream()
            resourceStream.CopyTo(ms)
            ms.Position = 0

            Dim reader As New WaveFileReader(ms)
            Dim sampleProvider = reader.ToSampleProvider()

            ' Приводим к формату микшера (44100 Hz, Stereo)
            If Not sampleProvider.WaveFormat.Equals(_mixer.WaveFormat) Then
                sampleProvider = New MediaFoundationResampler(
                    sampleProvider.ToWaveProvider(),
                    _mixer.WaveFormat
                ).ToSampleProvider()
            End If

            If Math.Abs(volume - 1.0F) > 0.001F Then
                sampleProvider = New VolumeSampleProvider(sampleProvider) With {.Volume = volume}
            End If

            _mixer.AddMixerInput(sampleProvider)
        End SyncLock
    End Sub

    ' Остановить все звуки (кроме фонового режима ReadFully)
    Public Sub StopAll()
        SyncLock _mixerLock
            _mixer.RemoveAllMixerInputs()
            _activeInputs.Clear()
        End SyncLock
    End Sub
    Public Function PlayStoppable(resourceData As Byte(), Optional volume As Single = 1.0F) As Guid
        Dim id = Guid.NewGuid()

        SyncLock _mixerLock
            Dim ms As New MemoryStream(resourceData)
            Dim reader As New WaveFileReader(ms)
            Dim sampleProvider = reader.ToSampleProvider()

            If Math.Abs(volume - 1.0F) > 0.001F Then
                sampleProvider = New VolumeSampleProvider(sampleProvider) With {.Volume = volume}
            End If

            ' Оборачиваем в провайдер, который можно удалить из микшера
            Dim tracked = New TrackedSampleProvider(sampleProvider, id)
            _activeInputs.Add(tracked)
            _mixer.AddMixerInput(tracked)
        End SyncLock

        Return id
    End Function

    ''' <summary>
    ''' Остановить конкретный звук по ID
    ''' </summary>
    Public Sub [Stop](id As Guid)
        SyncLock _mixerLock
            Dim input = _activeInputs.OfType(Of TrackedSampleProvider)().
            FirstOrDefault(Function(t) t.Id = id)

            If input IsNot Nothing Then
                _mixer.RemoveMixerInput(input)
                _activeInputs.Remove(input)
            End If
        End SyncLock
    End Sub

    ''' <summary>
    ''' Остановить всё и освободить ресурсы.
    ''' Вызывать при закрытии приложения.
    ''' </summary>
    Public Sub Shutdown()
        SyncLock _mixerLock
            _waveOut?.Stop()
            _waveOut?.Dispose()
        End SyncLock
    End Sub
    Private Function PrepareSampleProvider(source As ISampleProvider, volume As Single) As ISampleProvider
        ' Приведение к формату микшера
        If Not source.WaveFormat.Equals(_mixer.WaveFormat) Then
            source = New MediaFoundationResampler(
                source.ToWaveProvider(),
                _mixer.WaveFormat
            ).ToSampleProvider()
        End If

        ' Регулировка громкости
        If Math.Abs(volume - 1.0F) > 0.001F Then
            source = New VolumeSampleProvider(source) With {.Volume = volume}
        End If

        Return source
    End Function
End Class

''' <summary>
''' Обёртка вокруг ISampleProvider для идентификации и удаления из микшера
''' </summary>
Public Class TrackedSampleProvider
    Implements ISampleProvider

    Public ReadOnly Property Id As Guid
    Private ReadOnly _source As ISampleProvider

    Public Sub New(source As ISampleProvider, id As Guid)
        _source = source
        Me.Id = id
    End Sub

    Public ReadOnly Property WaveFormat As WaveFormat Implements ISampleProvider.WaveFormat
        Get
            Return _source.WaveFormat
        End Get
    End Property

    Public Function Read(buffer As Single(), offset As Integer, count As Integer) As Integer _
        Implements ISampleProvider.Read
        Return _source.Read(buffer, offset, count)
    End Function
End Class
