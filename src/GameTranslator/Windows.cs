using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingCopyPixelOperation = System.Drawing.CopyPixelOperation;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using Forms = System.Windows.Forms;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfPoint = System.Windows.Point;

namespace GameTranslator
{
    internal static class NativeMethods
    {
        internal const int GWL_EXSTYLE = -20;
        internal const int WS_EX_TRANSPARENT = 0x00000020;
        internal const int WS_EX_TOOLWINDOW = 0x00000080;
        internal const int WS_EX_NOACTIVATE = 0x08000000;
        internal const int WM_HOTKEY = 0x0312;
        internal const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
        internal static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        internal const uint SWP_NOSIZE = 0x0001;
        internal const uint SWP_NOMOVE = 0x0002;
        internal const uint SWP_NOACTIVATE = 0x0010;
        internal const uint SWP_FRAMECHANGED = 0x0020;
        internal const uint MOD_ALT = 0x0001;
        internal const uint MOD_CONTROL = 0x0002;
        internal const uint MOD_SHIFT = 0x0004;
        internal const uint MOD_WIN = 0x0008;
        internal const uint MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll")]
        internal static extern bool RegisterHotKey(
            IntPtr hWnd,
            int id,
            uint fsModifiers,
            uint vk);

        [DllImport("user32.dll")]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int index, int value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(
            IntPtr hWnd,
            int index,
            IntPtr value);

        [DllImport("user32.dll")]
        internal static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint flags);

        [DllImport("user32.dll")]
        internal static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);

        [DllImport("gdi32.dll")]
        internal static extern bool DeleteObject(IntPtr hObject);

        internal static long GetExtendedStyle(IntPtr handle)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(handle, GWL_EXSTYLE).ToInt64()
                : GetWindowLong32(handle, GWL_EXSTYLE);
        }

        internal static void SetExtendedStyle(IntPtr handle, long value)
        {
            if (IntPtr.Size == 8)
            {
                SetWindowLongPtr64(handle, GWL_EXSTYLE, new IntPtr(value));
            }
            else
            {
                SetWindowLong32(handle, GWL_EXSTYLE, (int)value);
            }
        }
    }

    public sealed class HotkeyService : IDisposable
    {
        private readonly int _hotkeyId;
        private readonly HwndSource _source;
        private IntPtr _handle;
        private bool _registered;
        private string _registeredText;

        public event EventHandler Pressed;
        public string RegisteredHotkey
        {
            get { return _registeredText; }
        }

        public HotkeyService(Window window, int hotkeyId)
        {
            _hotkeyId = hotkeyId;
            _handle = new WindowInteropHelper(window).Handle;
            _source = HwndSource.FromHwnd(_handle);
            if (_source != null)
            {
                _source.AddHook(WndProc);
            }
        }

        public bool Register(string text, out string error)
        {
            uint modifiers;
            uint key;
            if (!TryParse(text, out modifiers, out key))
            {
                error = "无法识别快捷键，请重新录制。";
                return false;
            }
            if (_registered
                && string.Equals(
                    _registeredText,
                    text,
                    StringComparison.OrdinalIgnoreCase))
            {
                error = "";
                return true;
            }

            var previousText = _registeredText;
            Unregister();
            _registered = NativeMethods.RegisterHotKey(
                _handle,
                _hotkeyId,
                modifiers | NativeMethods.MOD_NOREPEAT,
                key);
            if (_registered)
            {
                _registeredText = text;
                error = "";
                return true;
            }

            if (!string.IsNullOrWhiteSpace(previousText))
            {
                uint previousModifiers;
                uint previousKey;
                if (TryParse(
                        previousText,
                        out previousModifiers,
                        out previousKey))
                {
                    _registered = NativeMethods.RegisterHotKey(
                        _handle,
                        _hotkeyId,
                        previousModifiers | NativeMethods.MOD_NOREPEAT,
                        previousKey);
                    if (_registered)
                    {
                        _registeredText = previousText;
                    }
                }
            }
            error = "快捷键已被其他程序占用。";
            return false;
        }

        private static bool TryParse(string text, out uint modifiers, out uint key)
        {
            modifiers = 0;
            key = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var parts = text.Split('+');
            var keyText = "";
            foreach (var raw in parts)
            {
                var part = raw.Trim();
                if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= NativeMethods.MOD_ALT;
                }
                else if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)
                    || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= NativeMethods.MOD_CONTROL;
                }
                else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= NativeMethods.MOD_SHIFT;
                }
                else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase))
                {
                    modifiers |= NativeMethods.MOD_WIN;
                }
                else
                {
                    keyText = part;
                }
            }

            if (string.IsNullOrWhiteSpace(keyText))
            {
                return false;
            }

            try
            {
                var converter = new KeyConverter();
                var converted = converter.ConvertFromString(keyText);
                if (converted == null)
                {
                    return false;
                }
                var wpfKey = (Key)converted;
                key = (uint)KeyInterop.VirtualKeyFromKey(wpfKey);
                return key != 0 && wpfKey != Key.F12;
            }
            catch
            {
                if (keyText.Length == 1)
                {
                    var upper = char.ToUpperInvariant(keyText[0]);
                    key = upper;
                    return true;
                }
                return false;
            }
        }

        private IntPtr WndProc(
            IntPtr hwnd,
            int msg,
            IntPtr wParam,
            IntPtr lParam,
            ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == _hotkeyId)
            {
                var handler = Pressed;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Unregister()
        {
            if (_registered)
            {
                NativeMethods.UnregisterHotKey(_handle, _hotkeyId);
                _registered = false;
            }
            _registeredText = null;
        }

        public void Dispose()
        {
            Unregister();
            if (_source != null)
            {
                _source.RemoveHook(WndProc);
            }
        }
    }

    public sealed class OverlayWindow : Window
    {
        private readonly Border _panel;
        private readonly Border _editHeader;
        private readonly TextBlock _text;
        private readonly TextBlock _meta;
        private AppSettings _settings;
        private bool _editing;
        private bool _outputEnabled = true;
        private double _originalLeft;
        private double _originalTop;

        public event EventHandler PositionSaved;

        public OverlayWindow(AppSettings settings)
        {
            _settings = settings;
            Title = "翻译输出";
            Width = 520;
            MaxHeight = 420;
            SizeToContent = SizeToContent.Height;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = WpfBrushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = settings.OverlayLeft;
            Top = settings.OverlayTop;

            var root = new StackPanel();
            _editHeader = new Border
            {
                Background = new SolidColorBrush(WpfColor.FromRgb(255, 106, 0)),
                Padding = new Thickness(12, 6, 12, 6),
                Visibility = Visibility.Collapsed,
                Child = new TextBlock
                {
                    Text = "调整输出框位置  ·  拖动面板  ·  Enter 保存  ·  Esc 取消",
                    Foreground = WpfBrushes.White,
                    FontFamily = new WpfFontFamily("Consolas"),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold
                }
            };
            root.Children.Add(_editHeader);

            var content = new StackPanel { Margin = new Thickness(18, 14, 18, 16) };
            _meta = new TextBlock
            {
                Text = "就绪  ·  本地",
                Foreground = new SolidColorBrush(WpfColor.FromRgb(255, 106, 0)),
                FontFamily = new WpfFontFamily("Consolas"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            content.Children.Add(_meta);

            _text = new TextBlock
            {
                Text = "按下截图快捷键，框选需要翻译的游戏文字。",
                Foreground = new SolidColorBrush(WpfColor.FromRgb(21, 21, 21)),
                FontFamily = new WpfFontFamily("Microsoft YaHei UI"),
                FontSize = settings.FontSize,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = settings.FontSize * 1.45
            };
            content.Children.Add(_text);

            _panel = new Border
            {
                BorderBrush = new SolidColorBrush(WpfColor.FromRgb(21, 21, 21)),
                BorderThickness = new Thickness(1),
                Background = CreateBackground(settings.BackgroundOpacity),
                Child = content
            };
            root.Children.Add(_panel);
            Content = root;

            SourceInitialized += OnSourceInitialized;
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            KeyDown += OnKeyDown;
            Loaded += (sender, args) => ClampToScreen();
        }

        public void ApplySettings(AppSettings settings)
        {
            _settings = settings;
            _text.FontSize = settings.FontSize;
            _text.LineHeight = settings.FontSize * 1.45;
            _panel.Background = CreateBackground(settings.BackgroundOpacity);
            if (!_editing)
            {
                Left = settings.OverlayLeft;
                Top = settings.OverlayTop;
            }
            EnsureTopmost();
        }

        public void SetWorking(string message)
        {
            _meta.Text = "处理中  ·  " + message;
            _meta.Foreground = new SolidColorBrush(WpfColor.FromRgb(255, 106, 0));
            EnsureVisible();
        }

        public void SetResult(string text, string provider, TimeSpan duration)
        {
            _meta.Text = "就绪  ·  " + provider
                + "  ·  " + duration.TotalSeconds.ToString("0.00") + " 秒";
            _meta.Foreground = new SolidColorBrush(WpfColor.FromRgb(255, 106, 0));
            _text.Text = text;
            EnsureVisible();
        }

        public void SetError(string message)
        {
            _meta.Text = "发生错误  ·  请检查设置";
            _meta.Foreground = new SolidColorBrush(WpfColor.FromRgb(227, 59, 46));
            _text.Text = message;
            EnsureVisible();
        }

        public void ShowDefault()
        {
            EnsureVisible(true);
        }

        public bool ToggleOutputEnabled()
        {
            if (_editing)
            {
                return _outputEnabled;
            }
            _outputEnabled = !_outputEnabled;
            if (_outputEnabled)
            {
                EnsureVisible(true);
            }
            else if (IsVisible)
            {
                Hide();
            }
            return _outputEnabled;
        }

        public void EnterPositionEditMode()
        {
            _originalLeft = Left;
            _originalTop = Top;
            _editing = true;
            _editHeader.Visibility = Visibility.Visible;
            _panel.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(255, 106, 0));
            ShowActivated = true;
            EnsureVisible(true);
            SetClickThrough(false);
            Activate();
            Focus();
        }

        private void SavePosition()
        {
            _editing = false;
            _settings.OverlayLeft = Left;
            _settings.OverlayTop = Top;
            LeaveEditAppearance();
            var handler = PositionSaved;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void CancelPosition()
        {
            Left = _originalLeft;
            Top = _originalTop;
            _editing = false;
            LeaveEditAppearance();
        }

        private void LeaveEditAppearance()
        {
            _editHeader.Visibility = Visibility.Collapsed;
            _panel.BorderBrush = new SolidColorBrush(WpfColor.FromRgb(21, 21, 21));
            ShowActivated = false;
            SetClickThrough(true);
            if (_outputEnabled)
            {
                EnsureTopmost();
            }
            else
            {
                Hide();
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs args)
        {
            if (_editing && args.ButtonState == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch
                {
                }
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs args)
        {
            if (!_editing)
            {
                return;
            }
            if (args.Key == Key.Enter)
            {
                SavePosition();
                args.Handled = true;
            }
            else if (args.Key == Key.Escape)
            {
                CancelPosition();
                args.Handled = true;
            }
        }

        private void OnSourceInitialized(object sender, EventArgs args)
        {
            SetClickThrough(true);
            var handle = new WindowInteropHelper(this).Handle;
            NativeMethods.SetWindowDisplayAffinity(
                handle,
                NativeMethods.WDA_EXCLUDEFROMCAPTURE);
            EnsureTopmost();
        }

        private void SetClickThrough(bool enabled)
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var style = NativeMethods.GetExtendedStyle(handle);
            style |= NativeMethods.WS_EX_TOOLWINDOW;
            if (enabled)
            {
                style |= NativeMethods.WS_EX_TRANSPARENT;
                style |= NativeMethods.WS_EX_NOACTIVATE;
            }
            else
            {
                style &= ~NativeMethods.WS_EX_TRANSPARENT;
                style &= ~NativeMethods.WS_EX_NOACTIVATE;
            }
            NativeMethods.SetExtendedStyle(handle, style);
            NativeMethods.SetWindowPos(
                handle,
                NativeMethods.HWND_TOPMOST,
                0,
                0,
                0,
                0,
                NativeMethods.SWP_NOMOVE
                | NativeMethods.SWP_NOSIZE
                | NativeMethods.SWP_NOACTIVATE
                | NativeMethods.SWP_FRAMECHANGED);
        }

        private void EnsureVisible(bool force = false)
        {
            if (!_outputEnabled && !force)
            {
                return;
            }
            ClampToScreen();
            if (!IsVisible)
            {
                Show();
            }
            EnsureTopmost();
        }

        private void EnsureTopmost()
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                NativeMethods.SetWindowPos(
                    handle,
                    NativeMethods.HWND_TOPMOST,
                    0,
                    0,
                    0,
                    0,
                    NativeMethods.SWP_NOMOVE
                    | NativeMethods.SWP_NOSIZE
                    | NativeMethods.SWP_NOACTIVATE);
            }
        }

        private void ClampToScreen()
        {
            var virtualScreen = Forms.SystemInformation.VirtualScreen;
            var width = ActualWidth > 0 ? ActualWidth : Width;
            var height = ActualHeight > 0 ? ActualHeight : 160;
            Left = Math.Max(
                virtualScreen.Left,
                Math.Min(Left, virtualScreen.Right - width));
            Top = Math.Max(
                virtualScreen.Top,
                Math.Min(Top, virtualScreen.Bottom - height));
        }

        private static SolidColorBrush CreateBackground(double opacity)
        {
            var safe = Math.Max(0.05, Math.Min(0.95, opacity));
            return new SolidColorBrush(WpfColor.FromArgb(
                (byte)(255 * safe),
                243,
                240,
                232));
        }
    }

    public sealed class CaptureWindow : Window
    {
        private const double MagnifierImageWidth = 162;
        private const double MagnifierImageHeight = 108;
        private const double MagnifierFrameWidth = 168;
        private const double MagnifierFrameHeight = 114;
        private const double MagnifierGap = 22;

        private readonly DrawingBitmap _screenshot;
        private readonly BitmapSource _screenshotSource;
        private readonly Canvas _canvas;
        private readonly System.Windows.Shapes.Rectangle _selection;
        private readonly Border _magnifier;
        private readonly System.Windows.Controls.Image _magnifierImage;
        private readonly Ellipse _magnifierMarker;
        private bool _dragging;
        private WpfPoint _start;

        public event Action<DrawingBitmap> RegionSelected;

        public CaptureWindow()
        {
            var virtualScreen = Forms.SystemInformation.VirtualScreen;
            Title = "截图";
            _screenshot = new DrawingBitmap(
                virtualScreen.Width,
                virtualScreen.Height,
                DrawingPixelFormat.Format32bppArgb);
            using (var graphics = DrawingGraphics.FromImage(_screenshot))
            {
                graphics.CopyFromScreen(
                    virtualScreen.Left,
                    virtualScreen.Top,
                    0,
                    0,
                    virtualScreen.Size,
                    DrawingCopyPixelOperation.SourceCopy);
            }
            _screenshotSource = ToBitmapSource(_screenshot);

            Left = virtualScreen.Left;
            Top = virtualScreen.Top;
            Width = virtualScreen.Width;
            Height = virtualScreen.Height;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            Cursor = Cursors.Cross;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Background = WpfBrushes.Black;

            var grid = new Grid();
            var image = new System.Windows.Controls.Image
            {
                Source = _screenshotSource,
                Stretch = Stretch.Fill,
                Opacity = 0.76
            };
            grid.Children.Add(image);

            _canvas = new Canvas { Background = WpfBrushes.Transparent };
            _selection = new System.Windows.Shapes.Rectangle
            {
                Stroke = new SolidColorBrush(WpfColor.FromRgb(255, 106, 0)),
                StrokeThickness = 1.5,
                Fill = new SolidColorBrush(WpfColor.FromArgb(24, 255, 106, 0)),
                Visibility = Visibility.Collapsed
            };
            _canvas.Children.Add(_selection);

            _magnifierImage = new System.Windows.Controls.Image
            {
                Width = MagnifierImageWidth,
                Height = MagnifierImageHeight,
                Stretch = Stretch.Fill,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(
                _magnifierImage,
                BitmapScalingMode.NearestNeighbor);

            var magnifierContent = new Grid
            {
                Width = MagnifierImageWidth,
                Height = MagnifierImageHeight
            };
            magnifierContent.Children.Add(_magnifierImage);

            var markerCanvas = new Canvas
            {
                Width = MagnifierImageWidth,
                Height = MagnifierImageHeight,
                IsHitTestVisible = false
            };
            _magnifierMarker = new Ellipse
            {
                Width = 8,
                Height = 8,
                Stroke = new SolidColorBrush(
                    WpfColor.FromRgb(255, 106, 0)),
                StrokeThickness = 1.5,
                Fill = WpfBrushes.Transparent
            };
            markerCanvas.Children.Add(_magnifierMarker);
            magnifierContent.Children.Add(markerCanvas);

            _magnifier = new Border
            {
                Width = MagnifierFrameWidth,
                Height = MagnifierFrameHeight,
                Padding = new Thickness(2),
                Background = new SolidColorBrush(
                    WpfColor.FromRgb(243, 240, 232)),
                BorderBrush = new SolidColorBrush(
                    WpfColor.FromRgb(21, 21, 21)),
                BorderThickness = new Thickness(1),
                Child = magnifierContent,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true
            };
            Canvas.SetZIndex(_magnifier, 20);
            _canvas.Children.Add(_magnifier);
            grid.Children.Add(_canvas);

            var hint = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 24, 0, 0),
                Padding = new Thickness(16, 9, 16, 9),
                Background = new SolidColorBrush(WpfColor.FromRgb(243, 240, 232)),
                BorderBrush = new SolidColorBrush(WpfColor.FromRgb(21, 21, 21)),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = "拖动框选文字  ·  指针旁 3× 放大  ·  Esc 取消",
                    FontFamily = new WpfFontFamily("Consolas"),
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(WpfColor.FromRgb(21, 21, 21))
                }
            };
            grid.Children.Add(hint);
            Content = grid;

            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseMove += OnMouseMove;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            KeyDown += (sender, args) =>
            {
                if (args.Key == Key.Escape)
                {
                    Close();
                }
            };
            Closed += (sender, args) => _screenshot.Dispose();
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs args)
        {
            _dragging = true;
            _start = args.GetPosition(_canvas);
            UpdateMagnifier(_start);
            Canvas.SetLeft(_selection, _start.X);
            Canvas.SetTop(_selection, _start.Y);
            _selection.Width = 0;
            _selection.Height = 0;
            _selection.Visibility = Visibility.Visible;
            CaptureMouse();
        }

        private void OnMouseMove(object sender, MouseEventArgs args)
        {
            var current = args.GetPosition(_canvas);
            UpdateMagnifier(current);
            if (_dragging)
            {
                UpdateSelection(current);
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs args)
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            ReleaseMouseCapture();
            var current = args.GetPosition(_canvas);
            UpdateSelection(current);

            var left = Canvas.GetLeft(_selection);
            var top = Canvas.GetTop(_selection);
            var width = _selection.Width;
            var height = _selection.Height;
            if (width < 8 || height < 8)
            {
                _selection.Visibility = Visibility.Collapsed;
                return;
            }

            var scaleX = _screenshot.Width / Math.Max(1.0, ActualWidth);
            var scaleY = _screenshot.Height / Math.Max(1.0, ActualHeight);
            var crop = new System.Drawing.Rectangle(
                Math.Max(0, (int)Math.Round(left * scaleX)),
                Math.Max(0, (int)Math.Round(top * scaleY)),
                Math.Min(_screenshot.Width, (int)Math.Round(width * scaleX)),
                Math.Min(_screenshot.Height, (int)Math.Round(height * scaleY)));
            if (crop.Right > _screenshot.Width)
            {
                crop.Width = _screenshot.Width - crop.Left;
            }
            if (crop.Bottom > _screenshot.Height)
            {
                crop.Height = _screenshot.Height - crop.Top;
            }

            var selected = _screenshot.Clone(
                crop,
                DrawingPixelFormat.Format32bppArgb);
            var handler = RegionSelected;
            Close();
            if (handler != null)
            {
                handler(selected);
            }
            else
            {
                selected.Dispose();
            }
        }

        private void UpdateSelection(WpfPoint current)
        {
            var left = Math.Min(_start.X, current.X);
            var top = Math.Min(_start.Y, current.Y);
            var right = Math.Max(_start.X, current.X);
            var bottom = Math.Max(_start.Y, current.Y);
            Canvas.SetLeft(_selection, left);
            Canvas.SetTop(_selection, top);
            _selection.Width = right - left;
            _selection.Height = bottom - top;
        }

        private void UpdateMagnifier(WpfPoint current)
        {
            var canvasWidth = Math.Max(1.0, _canvas.ActualWidth);
            var canvasHeight = Math.Max(1.0, _canvas.ActualHeight);
            var scaleX = _screenshot.Width / canvasWidth;
            var scaleY = _screenshot.Height / canvasHeight;

            var sampleWidth = Math.Min(
                _screenshot.Width,
                Math.Max(12, (int)Math.Round(54 * scaleX)));
            var sampleHeight = Math.Min(
                _screenshot.Height,
                Math.Max(8, (int)Math.Round(36 * scaleY)));
            var pointerX = Math.Max(
                0,
                Math.Min(
                    _screenshot.Width - 1,
                    (int)Math.Round(current.X * scaleX)));
            var pointerY = Math.Max(
                0,
                Math.Min(
                    _screenshot.Height - 1,
                    (int)Math.Round(current.Y * scaleY)));
            var cropLeft = Math.Max(
                0,
                Math.Min(
                    pointerX - sampleWidth / 2,
                    _screenshot.Width - sampleWidth));
            var cropTop = Math.Max(
                0,
                Math.Min(
                    pointerY - sampleHeight / 2,
                    _screenshot.Height - sampleHeight));

            var cropped = new CroppedBitmap(
                _screenshotSource,
                new Int32Rect(
                    cropLeft,
                    cropTop,
                    sampleWidth,
                    sampleHeight));
            cropped.Freeze();
            _magnifierImage.Source = cropped;

            var markerX = (pointerX - cropLeft)
                * MagnifierImageWidth / sampleWidth;
            var markerY = (pointerY - cropTop)
                * MagnifierImageHeight / sampleHeight;
            Canvas.SetLeft(
                _magnifierMarker,
                Math.Max(
                    0,
                    Math.Min(MagnifierImageWidth - 8, markerX - 4)));
            Canvas.SetTop(
                _magnifierMarker,
                Math.Max(
                    0,
                    Math.Min(MagnifierImageHeight - 8, markerY - 4)));

            var magnifierLeft = current.X + MagnifierGap;
            var magnifierTop = current.Y + MagnifierGap;
            if (magnifierLeft + MagnifierFrameWidth > canvasWidth - 8)
            {
                magnifierLeft =
                    current.X - MagnifierFrameWidth - MagnifierGap;
            }
            if (magnifierTop + MagnifierFrameHeight > canvasHeight - 8)
            {
                magnifierTop =
                    current.Y - MagnifierFrameHeight - MagnifierGap;
            }
            magnifierLeft = Math.Max(
                8,
                Math.Min(
                    magnifierLeft,
                    canvasWidth - MagnifierFrameWidth - 8));
            magnifierTop = Math.Max(
                8,
                Math.Min(
                    magnifierTop,
                    canvasHeight - MagnifierFrameHeight - 8));

            Canvas.SetLeft(_magnifier, magnifierLeft);
            Canvas.SetTop(_magnifier, magnifierTop);
            _magnifier.Visibility = Visibility.Visible;
        }

        private static BitmapSource ToBitmapSource(DrawingBitmap bitmap)
        {
            var handle = bitmap.GetHbitmap();
            try
            {
                var source = Imaging.CreateBitmapSourceFromHBitmap(
                    handle,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                NativeMethods.DeleteObject(handle);
            }
        }
    }
}
