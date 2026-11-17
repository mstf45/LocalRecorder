using ScreenRecorderLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FormsScreen = System.Windows.Forms.Screen;

namespace LocalRecorder.Services;

public sealed class RecordingService : IDisposable
{
    private Recorder? _recorder;
    private bool _isRecording;
    private bool _isPaused;

    public bool IsRecording => _isRecording;
    public bool IsPaused => _isPaused;

    public event Action<string>? RecordingCompleted;
    public event Action<string>? RecordingFailed;

    public string StartRecording(
        int fps,
        bool recordMicrophone = false,
        bool recordSystemAudio = true,
        IntPtr? windowHandle = null,
        string? displayDeviceName = null,
        ScreenRect? sourceRect = null,
        bool gameCapture = false)
    {
        if (_isRecording)
        {
            throw new InvalidOperationException("Kayıt zaten devam ediyor.");
        }

        if (fps is not (30 or 60))
        {
            throw new ArgumentOutOfRangeException(
                nameof(fps),
                "FPS değeri 30 veya 60 olmalıdır.");
        }

        string recordingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            "LocalRecorder");

        Directory.CreateDirectory(recordingsDirectory);

        string filePath = Path.Combine(
            recordingsDirectory,
            $"Recording_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4");

        var audioSources = new List<AudioSourceBase>();

        if (recordSystemAudio)
        {
            var systemAudio = LoopbackAudioSource.Default;
            if (systemAudio is null)
            {
                throw new InvalidOperationException(
                    "Bilgisayar sesi için varsayılan hoparlör bulunamadı. Windows ses ayarlarından bir çıkış cihazı seçin.");
            }

            systemAudio.Volume = 1;
            audioSources.Add(systemAudio);
        }

        if (recordMicrophone)
        {
            var microphone = CaptureAudioSource.Default;
            if (microphone is null)
            {
                throw new InvalidOperationException(
                    "Varsayılan mikrofon bulunamadı. Windows ses ayarlarını kontrol edin.");
            }

            microphone.Volume = 1;
            audioSources.Add(microphone);
        }

        var audioOptions = new AudioOptions
        {
            IsAudioEnabled = audioSources.Count > 0,
            Channels = AudioChannels.Stereo,
            Bitrate = AudioBitrate.bitrate_192kbps,
            MasterVolume = 1,
            AudioSources = audioSources
        };

        var options = new RecorderOptions
        {
            VideoEncoderOptions = new VideoEncoderOptions
            {
                Encoder = new H264VideoEncoder
                {
                    BitrateMode = H264BitrateControlMode.Quality
                },
                Framerate = fps,
                Quality = 70,
                IsFixedFramerate = false,
                IsHardwareEncodingEnabled = false,
                IsLowLatencyEnabled = false,
                IsThrottlingDisabled = false,
                IsMp4FastStartEnabled = true
            },
            AudioOptions = audioOptions
        };

if (windowHandle.HasValue)
{
    if (!WindowSelectionService.IsWindowValid(windowHandle.Value))
    {
        throw new InvalidOperationException(
            "Seçilen pencere artık geçerli değil. Lütfen başka bir pencere seçin.");
    }

    var windowSource = new WindowRecordingSource(windowHandle.Value);
    if (gameCapture)
    {
        windowSource.IsBorderRequired = false;
    }

    options.SourceOptions = new SourceOptions
    {
        RecordingSources = { windowSource }
    };
}
else if (displayDeviceName is not null)
{
    var displaySource = new DisplayRecordingSource(displayDeviceName);
    if (gameCapture)
    {
        displaySource.RecorderApi = RecorderApi.WindowsGraphicsCapture;
        displaySource.IsBorderRequired = false;
    }

    options.SourceOptions = new SourceOptions
    {
        RecordingSources = { displaySource }
    };

    if (sourceRect is not null)
    {
        options.OutputOptions = new OutputOptions
        {
            RecorderMode = RecorderMode.Video,
            SourceRect = NormalizeSourceRect(sourceRect, displayDeviceName)
        };
    }
}
else if (sourceRect is not null)
{
    throw new InvalidOperationException(
        "Kırpılmış kayıt için bir ekran seçilmelidir.");
}

        _recorder = Recorder.CreateRecorder(options);

        _recorder.OnRecordingComplete += (_, _) =>
        {
            _isRecording = false;
            _isPaused = false;
            RecordingCompleted?.Invoke(filePath);
        };

        _recorder.OnRecordingFailed += (_, args) =>
        {
            _isRecording = false;
            _isPaused = false;
            RecordingFailed?.Invoke(args.Error);
        };

        _recorder.Record(filePath);

        _isRecording = true;
        _isPaused = false;

        return filePath;
    }

    private static ScreenRect NormalizeSourceRect(
        ScreenRect sourceRect,
        string displayDeviceName)
    {
        var screen = FormsScreen.AllScreens.FirstOrDefault(
            item => item.DeviceName == displayDeviceName);
        if (screen is null)
        {
            throw new InvalidOperationException(
                "Kayıt ekranı artık bağlı değil. Lütfen ekranı tekrar seçin.");
        }

        if (!double.IsFinite(sourceRect.Left) ||
            !double.IsFinite(sourceRect.Top) ||
            !double.IsFinite(sourceRect.Width) ||
            !double.IsFinite(sourceRect.Height) ||
            !double.IsFinite(sourceRect.Left + sourceRect.Width) ||
            !double.IsFinite(sourceRect.Top + sourceRect.Height))
        {
            throw new InvalidOperationException("Seçilen kayıt alanı geçersiz.");
        }

        double rightEdge = sourceRect.Left + sourceRect.Width;
        double bottomEdge = sourceRect.Top + sourceRect.Height;
        if (sourceRect.Left < 0 ||
            sourceRect.Top < 0 ||
            sourceRect.Width <= 0 ||
            sourceRect.Height <= 0 ||
            rightEdge > screen.Bounds.Width ||
            bottomEdge > screen.Bounds.Height)
        {
            throw new InvalidOperationException(
                "Seçilen alan ekran sınırlarının dışında. Lütfen alanı yeniden seçin.");
        }

        int left = (int)Math.Floor(sourceRect.Left);
        int top = (int)Math.Floor(sourceRect.Top);
        int right = (int)Math.Ceiling(rightEdge);
        int bottom = (int)Math.Ceiling(bottomEdge);

        if (left < 0 ||
            top < 0 ||
            right > screen.Bounds.Width ||
            bottom > screen.Bounds.Height)
        {
            throw new InvalidOperationException(
                "Seçilen alan ekran sınırlarının dışında. Lütfen alanı yeniden seçin.");
        }

        int width = right - left;
        int height = bottom - top;
        width -= width % 2;
        height -= height % 2;
        if (width < 2 || height < 2)
        {
            throw new InvalidOperationException(
                "Seçilen alan çok küçük. Lütfen daha geniş bir alan seçin.");
        }

        return new ScreenRect(left, top, width, height);
    }

    public void PauseRecording()
    {
        if (!_isRecording || _isPaused || _recorder is null)
        {
            return;
        }

        _recorder.Pause();
        _isPaused = true;
    }

    public void ResumeRecording()
    {
        if (!_isRecording || !_isPaused || _recorder is null)
        {
            return;
        }

        _recorder.Resume();
        _isPaused = false;
    }

    public void StopRecording()
    {
        if (!_isRecording || _recorder is null)
        {
            return;
        }

        _recorder.Stop();
    }

    public void Dispose()
    {
        _recorder?.Dispose();
        _recorder = null;
        _isRecording = false;
        _isPaused = false;
    }
}