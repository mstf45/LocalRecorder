using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MediaColor = System.Windows.Media.Color;
using LocalRecorder.Services;
using ScreenRecorderLib;
using WpfMessageBox = System.Windows.MessageBox;

namespace LocalRecorder;

public partial class MainWindow : Window
{
    private readonly RecordingService _recordingService = new();
    private readonly WindowSelectionService _windowSelectionService = new();

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _stopwatch = new();

    private bool _isPaused;
    private bool _highFrameRate;
    private string? _recordingPath;
    private ScreenRect? _selectedRegion;
    private string? _selectedRegionDeviceName;

    private enum CaptureMode
    {
        Display,
        Window,
        Region
    }

    private enum RecordingKind
    {
        Screen,
        Game
    }

    private sealed record RecordingKindOption(string Name, RecordingKind Kind)
    {
        public override string ToString() => Name;
    }

    private sealed record CaptureModeOption(
        string Name,
        CaptureMode Mode,
        string DeviceName)
    {
        public override string ToString() => Name;
    }

    private enum StatusTone
    {
        Idle,
        Live,
        Paused,
        Busy,
        Success,
        Error
    }

    public MainWindow()
    {
        InitializeComponent();

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _timer.Tick += (_, _) =>
        {
            TimerText.Text = _stopwatch.Elapsed.ToString(@"hh\:mm\:ss");
            UpdateSizeText();
        };

        _recordingService.RecordingCompleted += OnRecordingCompleted;
        _recordingService.RecordingFailed += OnRecordingFailed;

        Closed += (_, _) =>
        {
            _timer.Stop();
            _recordingService.Dispose();
        };

        RefreshCaptureModes();
        RefreshWindowList();
        RecordingKindComboBox.ItemsSource = new[]
        {
            new RecordingKindOption("Normal Ekran Kaydı", RecordingKind.Screen),
            new RecordingKindOption("Oyun Kaydı", RecordingKind.Game)
        };
        RecordingKindComboBox.SelectedIndex = 0;

        TimerText.Text = "00:00:00";
        UpdateSizeText();
        SetStatus("Hazır", StatusTone.Idle);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyDarkTitleBar();
        ApplyWindowIcon();
    }

    private void ApplyWindowIcon()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
        if (icon is null)
        {
            return;
        }

        Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed || IsInsideButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CaptureModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSourcePanels();
    }

    private void WindowComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CaptureModeComboBox.SelectedItem is CaptureModeOption { Mode: CaptureMode.Window })
        {
            UpdateSourceDetail();
        }
    }

    private void FpsButton_Click(object sender, RoutedEventArgs e)
    {
        _highFrameRate = !_highFrameRate;
        FpsValueText.Text = SelectedFps.ToString();
        FpsStatusText.Text = $"{SelectedFps} FPS";
    }

    private void SpeakerToggle_Click(object sender, RoutedEventArgs e)
    {
        SpeakerToggle.ToolTip = SpeakerToggle.IsChecked == true
            ? "Bilgisayar sesi açık"
            : "Bilgisayar sesi kapalı";
    }

    private void MicToggle_Click(object sender, RoutedEventArgs e)
    {
        MicToggle.ToolTip = MicToggle.IsChecked == true
            ? "Mikrofon açık"
            : "Mikrofon kapalı";
    }

    private void UpdateSourcePanels()
    {
        if (CaptureModeComboBox.SelectedItem is not CaptureModeOption option ||
            WindowPickerPanel is null ||
            SelectedAreaText is null)
        {
            return;
        }

        bool isWindow = option.Mode == CaptureMode.Window;
        bool isRegion = option.Mode == CaptureMode.Region;

        WindowPickerPanel.Visibility = isWindow ? Visibility.Visible : Visibility.Collapsed;

        if (!isRegion || option.DeviceName != _selectedRegionDeviceName)
        {
            _selectedRegion = null;
            _selectedRegionDeviceName = null;
            SelectedAreaText.Text = string.Empty;
        }

        if (isRegion && string.IsNullOrWhiteSpace(SelectedAreaText.Text))
        {
            SelectedAreaText.Text = "Henüz alan seçilmedi.";
        }

        SelectedAreaText.Visibility = isRegion ? Visibility.Visible : Visibility.Collapsed;
        CaptureModeComboBox.ToolTip = option.Name;
        UpdateSourceDetail();
    }

    private void UpdateSourceDetail()
    {
        if (CaptureModeComboBox.SelectedItem is not CaptureModeOption option || SourceDetailText is null)
        {
            return;
        }

        if (option.Mode == CaptureMode.Window)
        {
            var window = WindowComboBox.SelectedItem as WindowSelectionService.WindowInfo;
            SourceDetailText.Text = window is null
                ? "Pencere seçin"
                : $"Pencere - {window.Title}";
            return;
        }

        var screens = System.Windows.Forms.Screen.AllScreens;
        var screen = screens.FirstOrDefault(item => item.DeviceName == option.DeviceName);
        if (screen is null)
        {
            SourceDetailText.Text = "Ekran bağlı değil";
            return;
        }

        string label = screen.Primary
            ? "Ana ekran"
            : $"Ekran {Array.IndexOf(screens, screen) + 1}";
        string text =
            $"{screen.Bounds.Width}x{screen.Bounds.Height} - ({screen.Bounds.X}, {screen.Bounds.Y}) - {label}";

        if (option.Mode == CaptureMode.Region && _selectedRegion is not null)
        {
            text += $" · {_selectedRegion.Width:0}x{_selectedRegion.Height:0}";
        }
        else if (option.Mode == CaptureMode.Region)
        {
            text += " · alan seçilmedi";
        }

        SourceDetailText.Text = text;
    }

    private void RefreshCaptureModes()
    {
        string? selectedDevice = (CaptureModeComboBox.SelectedItem as CaptureModeOption)?.DeviceName;
        CaptureMode? selectedMode = (CaptureModeComboBox.SelectedItem as CaptureModeOption)?.Mode;
        var options = new System.Collections.Generic.List<CaptureModeOption>();
        var screens = System.Windows.Forms.Screen.AllScreens;

        foreach (var screen in screens)
        {
            string label = screen.Primary
                ? "Ana ekran"
                : $"Harici ekran {Array.IndexOf(screens, screen) + 1}";
            options.Add(new CaptureModeOption(
                $"Ekran: {label}",
                CaptureMode.Display,
                screen.DeviceName));
        }

        foreach (var screen in screens)
        {
            string label = screen.Primary
                ? "Ana ekran"
                : $"Harici ekran {Array.IndexOf(screens, screen) + 1}";
            options.Add(new CaptureModeOption(
                $"Belirli alan: {label}",
                CaptureMode.Region,
                screen.DeviceName));
        }

        options.Add(new CaptureModeOption("Seçilen pencere", CaptureMode.Window, string.Empty));
        CaptureModeComboBox.ItemsSource = options;
        CaptureModeComboBox.SelectedItem = options.FirstOrDefault(
            option => option.DeviceName == selectedDevice && option.Mode == selectedMode)
            ?? options.FirstOrDefault(option => option.Mode == CaptureMode.Display);
    }

    private void RefreshWindowsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshWindowList();
    }

    private void SelectAreaButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TrySelectRegionMode())
        {
            return;
        }

        if (CaptureModeComboBox.SelectedItem is not CaptureModeOption
            { Mode: CaptureMode.Region } option)
        {
            return;
        }

        var screen = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(
            item => item.DeviceName == option.DeviceName);
        if (screen is null)
        {
            WpfMessageBox.Show(
                "Seçilen ekran artık bağlı değil. Ekran listesini yenileyip tekrar deneyin.",
                "LocalRecorder",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            RefreshCaptureModes();
            return;
        }

        var selector = new RegionSelectionWindow(screen);
        if (selector.ShowDialog() == true && selector.SelectedRegion is not null)
        {
            _selectedRegion = selector.SelectedRegion;
            _selectedRegionDeviceName = option.DeviceName;
            string screenName = screen.Primary
                ? "Ana ekran"
                : $"Ekran {Array.IndexOf(System.Windows.Forms.Screen.AllScreens, screen) + 1}";
            SelectedAreaText.Text =
                $"{screenName} · {_selectedRegion.Width:0} × {_selectedRegion.Height:0} px";
            SelectedAreaText.Visibility = Visibility.Visible;
            UpdateSourceDetail();
        }
    }

    private bool TrySelectRegionMode()
    {
        if (CaptureModeComboBox.SelectedItem is CaptureModeOption { Mode: CaptureMode.Region })
        {
            return true;
        }

        string? device = (CaptureModeComboBox.SelectedItem as CaptureModeOption)?.DeviceName;
        if (string.IsNullOrWhiteSpace(device))
        {
            device = System.Windows.Forms.Screen.PrimaryScreen?.DeviceName;
        }

        var match = (CaptureModeComboBox.ItemsSource as System.Collections.IEnumerable)?
            .OfType<CaptureModeOption>()
            .FirstOrDefault(option => option.Mode == CaptureMode.Region && option.DeviceName == device);

        if (match is null)
        {
            return false;
        }

        CaptureModeComboBox.SelectedItem = match;
        return true;
    }

    private void RefreshWindowList()
    {
        if (WindowComboBox is null)
        {
            return;
        }

        var previousHandle =
            (WindowSelectionService.WindowInfo?)WindowComboBox.SelectedItem;

        var windows = _windowSelectionService.GetOpenWindows();

        WindowComboBox.ItemsSource = windows;

        if (previousHandle is not null)
        {
            WindowComboBox.SelectedItem = windows.FirstOrDefault(
                window => window.Handle == previousHandle.Handle);
        }

        if (WindowComboBox.SelectedIndex < 0 && windows.Count > 0)
        {
            WindowComboBox.SelectedIndex = 0;
        }
    }

    private void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_recordingService.IsRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    private void StartRecording()
    {
        bool gameCapture = RecordingKindComboBox.SelectedItem is RecordingKindOption
        {
            Kind: RecordingKind.Game
        };

        try
        {
            int fps = SelectedFps;

            IntPtr? windowHandle = null;
            string? displayDeviceName = null;
            ScreenRect? sourceRect = null;

            if (CaptureModeComboBox.SelectedItem is not CaptureModeOption captureOption)
            {
                throw new InvalidOperationException("Lütfen bir kayıt kaynağı seçin.");
            }

            if (captureOption.Mode == CaptureMode.Window)
            {
                if (WindowComboBox.SelectedItem is not
                    WindowSelectionService.WindowInfo selectedWindow)
                {
                    WpfMessageBox.Show(
                        "Lütfen kaydedilecek pencereyi seçin.",
                        "LocalRecorder",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!WindowSelectionService.IsWindowValid(selectedWindow.Handle))
                {
                    RefreshWindowList();

                    WpfMessageBox.Show(
                        "Seçilen pencere artık açık değil. Lütfen tekrar seçin.",
                        "LocalRecorder",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                windowHandle = selectedWindow.Handle;
            }
            else
            {
                displayDeviceName = captureOption.DeviceName;
                if (!System.Windows.Forms.Screen.AllScreens.Any(
                        screen => screen.DeviceName == displayDeviceName))
                {
                    RefreshCaptureModes();
                    throw new InvalidOperationException(
                        "Seçilen ekran artık bağlı değil. Lütfen kayıt kaynağını tekrar seçin.");
                }

                if (captureOption.Mode == CaptureMode.Region)
                {
                    if (_selectedRegion is null ||
                        _selectedRegionDeviceName != captureOption.DeviceName)
                    {
                        WpfMessageBox.Show(
                            "Önce alan düğmesiyle kayıt alanını seçin.",
                            "LocalRecorder",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    sourceRect = _selectedRegion;
                }
            }

            string filePath = _recordingService.StartRecording(
                fps: fps,
                recordMicrophone: MicToggle.IsChecked == true,
                recordSystemAudio: SpeakerToggle.IsChecked == true,
                windowHandle: windowHandle,
                displayDeviceName: displayDeviceName,
                sourceRect: sourceRect,
                gameCapture: gameCapture);

            _recordingPath = filePath;
            _stopwatch.Restart();
            _timer.Start();
            UpdateSizeText();

            _isPaused = false;
            ApplyTransportState(recording: true);
            SetInputsEnabled(false);
            SetStatus(gameCapture ? "Oyun kaydı" : "Kayıt", StatusTone.Live);
            StatusText.ToolTip = filePath;
        }
        catch (Exception ex)
        {
            SetStatus("Başlatılamadı", StatusTone.Error);

            string kindName = gameCapture ? "Oyun kaydı" : "Normal ekran kaydı";
            WpfMessageBox.Show(
                $"{kindName} başlatılırken hata oluştu:\n\n{ex.Message}",
                "LocalRecorder",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_isPaused)
            {
                _recordingService.PauseRecording();

                if (!_recordingService.IsPaused)
                {
                    return;
                }

                _stopwatch.Stop();
                _timer.Stop();

                _isPaused = true;
                PauseBars.Visibility = Visibility.Collapsed;
                ResumeIcon.Visibility = Visibility.Visible;
                PauseButton.ToolTip = "Devam et";
                SetStatus("Duraklatıldı", StatusTone.Paused);
            }
            else
            {
                _recordingService.ResumeRecording();

                if (_recordingService.IsPaused)
                {
                    return;
                }

                _stopwatch.Start();
                _timer.Start();

                _isPaused = false;
                PauseBars.Visibility = Visibility.Visible;
                ResumeIcon.Visibility = Visibility.Collapsed;
                PauseButton.ToolTip = "Duraklat";
                SetStatus("Kayıt", StatusTone.Live);
            }
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                $"Kayıt durumu değiştirilemedi:\n\n{ex.Message}",
                "LocalRecorder",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void StopRecording()
    {
        if (!_recordingService.IsRecording)
        {
            return;
        }

        RecordButton.IsEnabled = false;
        PauseButton.IsEnabled = false;
        SetStatus("Kaydediliyor...", StatusTone.Busy);

        _timer.Stop();
        _stopwatch.Stop();

        try
        {
            _recordingService.StopRecording();
        }
        catch (Exception ex)
        {
            RecordButton.IsEnabled = true;
            PauseButton.IsEnabled = true;
            SetStatus("Durdurulamadı", StatusTone.Error);

            WpfMessageBox.Show(
                $"Kayıt durdurulurken hata oluştu:\n\n{ex.Message}",
                "LocalRecorder",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnRecordingCompleted(string filePath)
    {
        Dispatcher.Invoke(() =>
        {
            _timer.Stop();
            _stopwatch.Stop();
            _recordingPath = null;
            UpdateSizeText();

            ApplyTransportState(recording: false);
            SetInputsEnabled(true);
            SetStatus("Tamamlandı", StatusTone.Success);

            WpfMessageBox.Show(
                $"Video başarıyla kaydedildi:\n\n{filePath}",
                "LocalRecorder",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        });
    }

    private void OnRecordingFailed(string error)
    {
        Dispatcher.Invoke(() =>
        {
            _timer.Stop();
            _stopwatch.Stop();
            _recordingPath = null;
            UpdateSizeText();

            ApplyTransportState(recording: false);
            SetInputsEnabled(true);
            SetStatus("Başarısız", StatusTone.Error);

            WpfMessageBox.Show(
                $"Kayıt sırasında hata oluştu:\n\n{error}",
                "LocalRecorder",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        });
    }

    private void SetInputsEnabled(bool enabled)
    {
        CaptureModeComboBox.IsEnabled = enabled;
        RecordingKindComboBox.IsEnabled = enabled;
        WindowComboBox.IsEnabled = enabled;
        RefreshWindowsButton.IsEnabled = enabled;
        SelectAreaButton.IsEnabled = enabled;
        FpsButton.IsEnabled = enabled;
        SpeakerToggle.IsEnabled = enabled;
        MicToggle.IsEnabled = enabled;
    }

    private void ApplyTransportState(bool recording)
    {
        StopGlyph.Visibility = recording ? Visibility.Visible : Visibility.Collapsed;
        RecordButton.ToolTip = recording ? "Kaydı durdur" : "Kaydı başlat";
        RecordButton.IsEnabled = true;
        PauseButton.IsEnabled = recording;

        if (!recording)
        {
            _isPaused = false;
            PauseBars.Visibility = Visibility.Visible;
            ResumeIcon.Visibility = Visibility.Collapsed;
            PauseButton.ToolTip = "Duraklat";
        }
    }

    private void SetStatus(string message, StatusTone tone)
    {
        StatusText.Text = message;
        StatusText.Foreground = new SolidColorBrush(tone switch
        {
            StatusTone.Live => MediaColor.FromRgb(255, 255, 255),
            StatusTone.Paused => MediaColor.FromRgb(245, 193, 108),
            StatusTone.Busy => MediaColor.FromRgb(168, 198, 255),
            StatusTone.Success => MediaColor.FromRgb(157, 219, 190),
            StatusTone.Error => MediaColor.FromRgb(255, 142, 142),
            _ => MediaColor.FromRgb(213, 222, 232)
        });
    }

    private void UpdateSizeText()
    {
        long written = 0;
        if (!string.IsNullOrEmpty(_recordingPath) && File.Exists(_recordingPath))
        {
            try
            {
                written = new FileInfo(_recordingPath).Length;
            }
            catch (IOException)
            {
                written = 0;
            }
        }

        string free = "--";
        try
        {
            string? root = Path.GetPathRoot(RecordingsDirectory);
            if (!string.IsNullOrEmpty(root))
            {
                free = FormatSize(new DriveInfo(root).AvailableFreeSpace);
            }
        }
        catch (IOException)
        {
            free = "--";
        }
        catch (UnauthorizedAccessException)
        {
            free = "--";
        }

        SizeText.Text = $"{FormatSize(written)} / {free}";
    }

    private int SelectedFps => _highFrameRate ? 60 : 30;

    private static string RecordingsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        "LocalRecorder");

    private static string FormatSize(long bytes)
    {
        double kilobytes = bytes / 1024d;
        if (kilobytes < 1024)
        {
            return $"{kilobytes:0.0} KB";
        }

        double megabytes = kilobytes / 1024d;
        if (megabytes < 1024)
        {
            return $"{megabytes:0.0} MB";
        }

        return $"{megabytes / 1024d:0.0} GB";
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void ApplyDarkTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        int dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
