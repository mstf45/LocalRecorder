using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenRecorderLib;
using FormsScreen = System.Windows.Forms.Screen;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using WpfColor = System.Windows.Media.Color;
using WpfCursors = System.Windows.Input.Cursors;
using WpfEllipse = System.Windows.Shapes.Ellipse;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfPoint = System.Windows.Point;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfButton = System.Windows.Controls.Button;
using WpfMessageBox = System.Windows.MessageBox;
using WpfVisualTreeHelper = System.Windows.Media.VisualTreeHelper;

namespace LocalRecorder;

public sealed class RegionSelectionWindow : Window
{
    private readonly Canvas _canvas;
    private readonly FormsScreen _screen;
    private WpfPoint _dragStart;
    private WpfRectangle? _selectionRectangle;
    private readonly System.Collections.Generic.List<WpfEllipse> _resizeHandles = new();
    private DragMode _dragMode;
    private int _activeHandle = -1;
    private WpfPoint _originalTopLeft;
    private WpfPoint _originalBottomRight;

    private enum DragMode
    {
        None,
        Selecting,
        Moving,
        Resizing
    }

    public ScreenRect? SelectedRegion { get; private set; }

    public RegionSelectionWindow(FormsScreen screen)
    {
        _screen = screen;
        var dpiScale = GetDpiScale(screen);

        Title = "Kayıt alanını seçin";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Topmost = true;
        ShowInTaskbar = false;
        AllowsTransparency = true;
        Background = new SolidColorBrush(WpfColor.FromArgb(120, 8, 10, 14));
        Left = screen.Bounds.Left / dpiScale.X;
        Top = screen.Bounds.Top / dpiScale.Y;
        Width = screen.Bounds.Width / dpiScale.X;
        Height = screen.Bounds.Height / dpiScale.Y;
        Cursor = WpfCursors.Cross;

        var root = new Grid();
        _canvas = new Canvas { Background = Background };
        root.Children.Add(_canvas);

        FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

        var screens = FormsScreen.AllScreens;
        int screenNumber = Array.IndexOf(screens, screen) + 1;
        string screenLabel = screen.Primary
            ? "Ana ekran"
            : $"Harici ekran {screenNumber}";
        var screenInfo = new Border
        {
            Background = new SolidColorBrush(WpfColor.FromArgb(230, 20, 25, 34)),
            BorderBrush = new SolidColorBrush(WpfColor.FromArgb(255, 38, 44, 56)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14, 10, 14, 10),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(16),
            Child = new TextBlock
            {
                Text = $"{screenLabel}  ·  {screen.Bounds.Width} × {screen.Bounds.Height}",
                Foreground = WpfBrushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 15
            }
        };
        root.Children.Add(screenInfo);

        var controls = new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        controls.Children.Add(new TextBlock
        {
            Text = "Fareyle alanı çizin, ardından kaydedin",
            Foreground = new SolidColorBrush(WpfColor.FromRgb(197, 203, 214)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 14, 0)
        });
        var saveButton = CreateOverlayButton("Alanı kaydet", primary: true);
        saveButton.Click += (_, _) => SaveSelection();
        controls.Children.Add(saveButton);
        var cancelButton = CreateOverlayButton("Vazgeç", primary: false);
        cancelButton.Click += (_, _) => DialogResult = false;
        controls.Children.Add(cancelButton);
        var controlBar = new Border
        {
            Background = new SolidColorBrush(WpfColor.FromArgb(230, 20, 25, 34)),
            BorderBrush = new SolidColorBrush(WpfColor.FromArgb(255, 38, 44, 56)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 8, 10, 8),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(16),
            Child = controls
        };
        root.Children.Add(controlBar);

        Content = root;
        PreviewMouseLeftButtonDown += OnMouseLeftButtonDown;
        PreviewMouseMove += OnMouseMove;
        PreviewMouseLeftButtonUp += OnMouseLeftButtonUp;
        Loaded += (_, _) => Focus();
        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                DialogResult = false;
            }
            else if (args.Key == Key.Enter)
            {
                SaveSelection();
            }
        };
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsButtonSource(e.OriginalSource as DependencyObject))
        {
            return;
        }

        _dragStart = e.GetPosition(_canvas);
        var position = e.GetPosition(_canvas);
        _activeHandle = FindHandle(position);
        if (_activeHandle >= 0)
        {
            CaptureOriginalBounds(position);
            _dragMode = DragMode.Resizing;
        }
        else if (IsInsideSelection(position))
        {
            CaptureOriginalBounds(position);
            _dragMode = DragMode.Moving;
        }
        else
        {
            ClearSelection();
            _dragStart = position;
            _selectionRectangle = new WpfRectangle
            {
                Stroke = new SolidColorBrush(WpfColor.FromRgb(134, 239, 172)),
                StrokeThickness = 3,
                Fill = new SolidColorBrush(WpfColor.FromArgb(18, 134, 239, 172)),
                SnapsToDevicePixels = true
            };
            Canvas.SetLeft(_selectionRectangle, _dragStart.X);
            Canvas.SetTop(_selectionRectangle, _dragStart.Y);
            _canvas.Children.Add(_selectionRectangle);
            _dragMode = DragMode.Selecting;
        }

        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_dragMode == DragMode.None || _selectionRectangle is null)
        {
            return;
        }

        var current = e.GetPosition(_canvas);
        if (_dragMode == DragMode.Selecting)
        {
            SetSelectionBounds(
                new WpfPoint(Math.Min(_dragStart.X, current.X), Math.Min(_dragStart.Y, current.Y)),
                new WpfPoint(Math.Max(_dragStart.X, current.X), Math.Max(_dragStart.Y, current.Y)));
            e.Handled = true;
            return;
        }

        double left = _originalTopLeft.X;
        double top = _originalTopLeft.Y;
        double right = _originalBottomRight.X;
        double bottom = _originalBottomRight.Y;

        if (_dragMode == DragMode.Moving)
        {
            double dx = current.X - _dragStart.X;
            double dy = current.Y - _dragStart.Y;
            left += dx;
            right += dx;
            top += dy;
            bottom += dy;
        }
        else
        {
            ResizeBounds(current, ref left, ref top, ref right, ref bottom);
        }

        SetSelectionBounds(
            new WpfPoint(Math.Max(0, left), Math.Max(0, top)),
            new WpfPoint(Math.Min(_canvas.ActualWidth, right), Math.Min(_canvas.ActualHeight, bottom)));
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragMode == DragMode.None)
        {
            return;
        }

        _dragMode = DragMode.None;
        ReleaseMouseCapture();
        UpdateResizeHandles();
        e.Handled = true;
    }

    private void SaveSelection()
    {
        if (_selectionRectangle is null ||
            _selectionRectangle.Width < 2 ||
            _selectionRectangle.Height < 2)
        {
            WpfMessageBox.Show(
                "Kaydetmeden önce ekranda bir alan seçin.",
                "LocalRecorder",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var topLeft = _canvas.PointToScreen(new WpfPoint(
            Canvas.GetLeft(_selectionRectangle),
            Canvas.GetTop(_selectionRectangle)));
        var bottomRight = _canvas.PointToScreen(new WpfPoint(
            Canvas.GetLeft(_selectionRectangle) + _selectionRectangle.Width,
            Canvas.GetTop(_selectionRectangle) + _selectionRectangle.Height));

        double left = Math.Max(0, topLeft.X - _screen.Bounds.Left);
        double top = Math.Max(0, topLeft.Y - _screen.Bounds.Top);
        double right = Math.Min(_screen.Bounds.Width, bottomRight.X - _screen.Bounds.Left);
        double bottom = Math.Min(_screen.Bounds.Height, bottomRight.Y - _screen.Bounds.Top);
        SelectedRegion = new ScreenRect(left, top, right - left, bottom - top);
        DialogResult = true;
    }

    private void ClearSelection()
    {
        _canvas.Children.Clear();
        _resizeHandles.Clear();
        _selectionRectangle = null;
    }

    private static bool IsButtonSource(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is WpfButtonBase)
            {
                return true;
            }

            source = WpfVisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void CaptureOriginalBounds(WpfPoint position)
    {
        if (_selectionRectangle is null)
        {
            return;
        }

        _originalTopLeft = new WpfPoint(
            Canvas.GetLeft(_selectionRectangle),
            Canvas.GetTop(_selectionRectangle));
        _originalBottomRight = new WpfPoint(
            _originalTopLeft.X + _selectionRectangle.Width,
            _originalTopLeft.Y + _selectionRectangle.Height);
        _dragStart = position;
    }

    private bool IsInsideSelection(WpfPoint point)
    {
        if (_selectionRectangle is null)
        {
            return false;
        }

        return point.X >= Canvas.GetLeft(_selectionRectangle) &&
            point.X <= Canvas.GetLeft(_selectionRectangle) + _selectionRectangle.Width &&
            point.Y >= Canvas.GetTop(_selectionRectangle) &&
            point.Y <= Canvas.GetTop(_selectionRectangle) + _selectionRectangle.Height;
    }

    private int FindHandle(WpfPoint point)
    {
        for (int i = 0; i < _resizeHandles.Count; i++)
        {
            double x = Canvas.GetLeft(_resizeHandles[i]) + _resizeHandles[i].Width / 2;
            double y = Canvas.GetTop(_resizeHandles[i]) + _resizeHandles[i].Height / 2;
            if (Math.Abs(point.X - x) <= 9 && Math.Abs(point.Y - y) <= 9)
            {
                return i;
            }
        }

        return -1;
    }

    private void ResizeBounds(WpfPoint current, ref double left, ref double top, ref double right, ref double bottom)
    {
        bool leftSide = _activeHandle is 0 or 3 or 5;
        bool rightSide = _activeHandle is 2 or 4 or 7;
        bool topSide = _activeHandle is 0 or 1 or 2;
        bool bottomSide = _activeHandle is 5 or 6 or 7;

        if (leftSide) left = Math.Min(current.X, right - 2);
        if (rightSide) right = Math.Max(current.X, left + 2);
        if (topSide) top = Math.Min(current.Y, bottom - 2);
        if (bottomSide) bottom = Math.Max(current.Y, top + 2);
    }

    private void SetSelectionBounds(WpfPoint topLeft, WpfPoint bottomRight)
    {
        if (_selectionRectangle is null)
        {
            return;
        }

        Canvas.SetLeft(_selectionRectangle, topLeft.X);
        Canvas.SetTop(_selectionRectangle, topLeft.Y);
        _selectionRectangle.Width = Math.Max(0, bottomRight.X - topLeft.X);
        _selectionRectangle.Height = Math.Max(0, bottomRight.Y - topLeft.Y);
        UpdateResizeHandles();
    }

    private void UpdateResizeHandles()
    {
        if (_selectionRectangle is null)
        {
            return;
        }

        if (_resizeHandles.Count == 0)
        {
            for (int i = 0; i < 8; i++)
            {
                var handle = new WpfEllipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = new SolidColorBrush(WpfColor.FromRgb(167, 243, 208)),
                    Stroke = new SolidColorBrush(WpfColor.FromRgb(5, 46, 22)),
                    StrokeThickness = 1,
                    Cursor = WpfCursors.SizeAll
                };
                _resizeHandles.Add(handle);
                _canvas.Children.Add(handle);
            }
        }

        double left = Canvas.GetLeft(_selectionRectangle);
        double top = Canvas.GetTop(_selectionRectangle);
        double right = left + _selectionRectangle.Width;
        double bottom = top + _selectionRectangle.Height;
        double centerX = (left + right) / 2;
        double centerY = (top + bottom) / 2;
        var positions = new[]
        {
            new WpfPoint(left, top), new WpfPoint(centerX, top), new WpfPoint(right, top),
            new WpfPoint(left, centerY), new WpfPoint(right, centerY),
            new WpfPoint(left, bottom), new WpfPoint(centerX, bottom), new WpfPoint(right, bottom)
        };

        for (int i = 0; i < _resizeHandles.Count; i++)
        {
            Canvas.SetLeft(_resizeHandles[i], positions[i].X - _resizeHandles[i].Width / 2);
            Canvas.SetTop(_resizeHandles[i], positions[i].Y - _resizeHandles[i].Height / 2);
        }
    }

    private static WpfButton CreateOverlayButton(string content, bool primary)
    {
        return new WpfButton
        {
            Content = content,
            Margin = new Thickness(4, 0, 4, 0),
            Padding = new Thickness(16, 8, 16, 8),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Cursor = WpfCursors.Hand,
            Foreground = primary
                ? new SolidColorBrush(WpfColor.FromRgb(6, 33, 22))
                : WpfBrushes.White,
            Background = primary
                ? new SolidColorBrush(WpfColor.FromRgb(61, 220, 151))
                : new SolidColorBrush(WpfColor.FromRgb(26, 32, 48)),
            BorderBrush = primary
                ? new SolidColorBrush(WpfColor.FromRgb(61, 220, 151))
                : new SolidColorBrush(WpfColor.FromRgb(44, 53, 72)),
            BorderThickness = new Thickness(1),
            Template = CreateRoundButtonTemplate()
        };
    }

    private static ControlTemplate CreateRoundButtonTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(WpfButton.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(WpfButton.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(WpfButton.BorderThicknessProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(WpfButton.PaddingProperty));

        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);

        return new ControlTemplate(typeof(WpfButton))
        {
            VisualTree = border
        };
    }

    private static (double X, double Y) GetDpiScale(FormsScreen screen)
    {
        var point = new NativePoint
        {
            X = screen.Bounds.Left + screen.Bounds.Width / 2,
            Y = screen.Bounds.Top + screen.Bounds.Height / 2
        };
        IntPtr monitor = MonitorFromPoint(point, 2);
        if (monitor != IntPtr.Zero &&
            GetDpiForMonitor(monitor, 0, out uint dpiX, out uint dpiY) == 0)
        {
            return (dpiX / 96d, dpiY / 96d);
        }

        return (1, 1);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(
        IntPtr monitor,
        int dpiType,
        out uint dpiX,
        out uint dpiY);
}
