using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace GameTranslator
{
    public sealed class MainWindow : Window
    {
        private readonly SettingsStore _settingsStore;
        private readonly IOcrEngine _ocrEngine;
        private AppSettings _settings;
        private OverlayWindow _overlay;
        private HotkeyService _hotkeyService;
        private HotkeyService _toggleHotkeyService;
        private HotkeyService _textReplacementHotkeyService;
        private Forms.NotifyIcon _trayIcon;
        private Icon _appIcon;
        private CancellationTokenSource _workCancellation;
        private CancellationTokenSource _textReplacementCancellation;
        private HotkeyRecordingTarget _recordingHotkey;
        private bool _initializing;
        private bool _initialOverlayShown;
        private bool _exiting;
        private bool _textReplacementBusy;
        private CaptureWindow _captureWindow;

        private FrameworkElement _root;
        private TextBlock _headerStatus;
        private TextBlock _footerStatus;
        private Button _hotkeyButton;
        private Button _toggleHotkeyButton;
        private Slider _fontSizeSlider;
        private Slider _opacitySlider;
        private TextBlock _fontSizeValue;
        private TextBlock _opacityValue;
        private TextBlock _translationProviderName;
        private TextBlock _translationProviderDescription;
        private Button _translationProviderButton;
        private Button _textReplacementButton;
        private TextBlock _textReplacementHotkeyValue;
        private TextBlock _textReplacementTargetValue;
        private System.Windows.Controls.Image _headerLogo;

        private enum HotkeyRecordingTarget
        {
            None,
            Capture,
            ToggleOutput
        }

        public MainWindow()
        {
            _settingsStore = new SettingsStore();
            _settings = _settingsStore.Load();
            _ocrEngine = new WindowsOcrEngine();

            Title = "游戏翻译器";
            Width = 720;
            Height = 560;
            MinWidth = 680;
            MinHeight = 530;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = System.Windows.Media.Brushes.White;

            LoadLayout();
            LoadBranding();
            BindControls();
            ApplySettingsToControls();
            ConfigureTray();

            SourceInitialized += OnSourceInitialized;
            Closing += OnClosing;
            PreviewKeyDown += OnPreviewKeyDown;
            Loaded += (sender, args) =>
            {
                if (_initialOverlayShown)
                {
                    return;
                }
                _initialOverlayShown = true;
                EnsureOverlay();
                _overlay.ShowDefault();
            };
        }

        private void LoadBranding()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var pngPath = Path.Combine(baseDirectory, "logo.png");
            var icoPath = Path.Combine(baseDirectory, "logo.ico");

            if (File.Exists(icoPath))
            {
                Icon = BitmapFrame.Create(new Uri(icoPath, UriKind.Absolute));
                _appIcon = new Icon(icoPath);
            }

            _headerLogo = Find<System.Windows.Controls.Image>("HeaderLogo");
            if (File.Exists(pngPath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(pngPath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                _headerLogo.Source = bitmap;
            }
        }

        private void LoadLayout()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MainWindow.xaml");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("缺少界面文件 MainWindow.xaml。", path);
            }

            using (var stream = File.OpenRead(path))
            {
                _root = XamlReader.Load(stream) as FrameworkElement;
            }
            if (_root == null)
            {
                throw new InvalidOperationException("无法载入主界面。");
            }
            Content = _root;
        }

        private void BindControls()
        {
            _headerStatus = Find<TextBlock>("HeaderStatus");
            _footerStatus = Find<TextBlock>("FooterStatus");
            _hotkeyButton = Find<Button>("HotkeyButton");
            _toggleHotkeyButton = Find<Button>("ToggleHotkeyButton");
            _fontSizeSlider = Find<Slider>("FontSizeSlider");
            _opacitySlider = Find<Slider>("OpacitySlider");
            _fontSizeValue = Find<TextBlock>("FontSizeValue");
            _opacityValue = Find<TextBlock>("OpacityValue");
            _translationProviderName =
                Find<TextBlock>("TranslationProviderName");
            _translationProviderDescription =
                Find<TextBlock>("TranslationProviderDescription");
            _translationProviderButton =
                Find<Button>("TranslationProviderButton");
            _textReplacementButton =
                Find<Button>("TextReplacementButton");
            _textReplacementHotkeyValue =
                Find<TextBlock>("TextReplacementHotkeyValue");
            _textReplacementTargetValue =
                Find<TextBlock>("TextReplacementTargetValue");

            _hotkeyButton.Click += (sender, args) =>
            {
                BeginHotkeyRecording(HotkeyRecordingTarget.Capture);
            };
            _toggleHotkeyButton.Click += (sender, args) =>
            {
                BeginHotkeyRecording(HotkeyRecordingTarget.ToggleOutput);
            };
            _fontSizeSlider.ValueChanged += (sender, args) =>
            {
                if (_fontSizeValue != null)
                {
                    _fontSizeValue.Text = ((int)_fontSizeSlider.Value) + " 像素";
                }
                ApplyLiveOutputSettings();
            };
            _opacitySlider.ValueChanged += (sender, args) =>
            {
                if (_opacityValue != null)
                {
                    _opacityValue.Text = ((int)_opacitySlider.Value) + " %";
                }
                ApplyLiveOutputSettings();
            };

            Find<Button>("AdjustPositionButton").Click += (sender, args) =>
            {
                EnsureOverlay();
                _overlay.EnterPositionEditMode();
                _footerStatus.Text = "拖动输出框，按 Enter 保存";
            };
            Find<Button>("PreviewButton").Click += (sender, args) =>
            {
                EnsureOverlay();
                _overlay.SetResult(
                    "这里会显示截图翻译结果。\n拖动位置后，输出框会始终保持在游戏画面前方。",
                    "预览",
                    TimeSpan.FromSeconds(0.86));
            };
            Find<Button>("CaptureButton").Click += (sender, args) => StartCapture(true);
            Find<Button>("SaveButton").Click += (sender, args) => SaveSettings(true);
            _translationProviderButton.Click +=
                (sender, args) => OpenTranslationSettings();
            _textReplacementButton.Click +=
                (sender, args) => OpenTextReplacementSettings();
        }

        private void ApplySettingsToControls()
        {
            _initializing = true;
            _hotkeyButton.Content = _settings.Hotkey.ToUpperInvariant().Replace("+", " + ");
            _toggleHotkeyButton.Content = _settings.ToggleOverlayHotkey
                .ToUpperInvariant()
                .Replace("+", " + ");
            _fontSizeSlider.Value = _settings.FontSize;
            _opacitySlider.Value = _settings.BackgroundOpacity * 100;
            _fontSizeValue.Text = ((int)_settings.FontSize) + " 像素";
            _opacityValue.Text = ((int)(_settings.BackgroundOpacity * 100)) + " %";
            UpdateTranslationProviderSummary();
            UpdateTextReplacementSummary();
            _initializing = false;
        }

        private void OpenTextReplacementSettings()
        {
            var window = new TextReplacementSettingsWindow(this, _settings);
            var result = window.ShowDialog();
            if (result != true || !window.SettingsSaved)
            {
                return;
            }

            var oldHotkey = _settings.TextReplacementHotkey;
            var oldTarget = _settings.TextReplacementTargetLanguage;
            _settings.TextReplacementHotkey = window.SelectedHotkey;
            _settings.TextReplacementTargetLanguage =
                window.SelectedTargetLanguage;
            if (!RegisterHotkeys())
            {
                _settings.TextReplacementHotkey = oldHotkey;
                _settings.TextReplacementTargetLanguage = oldTarget;
                RegisterHotkeys();
                SetStatus("文本替换快捷键注册失败，已恢复原设置。", true);
                return;
            }

            if (!TrySaveSettings())
            {
                return;
            }
            UpdateTextReplacementSummary();
            SetStatus(
                "文本替换已设置为 "
                + _settings.TextReplacementHotkey
                + " → "
                + TranslationLanguages.GetDisplayName(
                    _settings.TextReplacementTargetLanguage),
                false);
        }

        private void UpdateTextReplacementSummary()
        {
            _textReplacementHotkeyValue.Text =
                _settings.TextReplacementHotkey.ToUpperInvariant()
                    .Replace("+", " + ");
            _textReplacementTargetValue.Text =
                TranslationLanguages.GetDisplayName(
                    _settings.TextReplacementTargetLanguage);
        }

        private void OpenTranslationSettings()
        {
            var window = new TranslationSettingsWindow(this, _settings);
            var result = window.ShowDialog();
            if (result != true || !window.SettingsSaved)
            {
                return;
            }

            if (!TrySaveSettings())
            {
                return;
            }
            UpdateTranslationProviderSummary();
            SetStatus(
                "翻译接口已切换为：" + _settings.TranslationProvider,
                false);
        }

        private void UpdateTranslationProviderSummary()
        {
            _translationProviderName.Text = _settings.TranslationProvider;
            switch (_settings.TranslationProvider)
            {
                case "DeepL API":
                    _translationProviderDescription.Text =
                        "质量优先 · 使用 DeepL API 密钥";
                    break;
                case "Microsoft Translator":
                    _translationProviderDescription.Text =
                        "国内通常可直连 · 支持 Azure 自定义端点";
                    break;
                case "Google 在线翻译":
                    _translationProviderDescription.Text =
                        "Google 网页翻译直连 · 无需密钥";
                    break;
                case "Lingva Translate":
                    _translationProviderDescription.Text =
                        "公共或自建节点 · 无需密钥";
                    break;
                case "LibreTranslate 免密钥":
                    _translationProviderDescription.Text =
                        "免密钥公共或自建节点";
                    break;
                case "LibreTranslate API":
                    _translationProviderDescription.Text =
                        "开源接口 · 使用 API 密钥";
                    break;
                case "MyMemory 免费翻译":
                    _translationProviderDescription.Text =
                        "免费直连 · 无需密钥";
                    break;
                default:
                    _translationProviderName.Text = "智能在线翻译";
                    _translationProviderDescription.Text =
                        "本地游戏词典 · Google 优先 · 自动备用";
                    break;
            }
        }

        private void OnSourceInitialized(object sender, EventArgs args)
        {
            _hotkeyService = new HotkeyService(this, 0x4711);
            _toggleHotkeyService = new HotkeyService(this, 0x4712);
            _textReplacementHotkeyService =
                new HotkeyService(this, 0x4713);
            _hotkeyService.Pressed += (hotkeySender, hotkeyArgs) => StartCapture(false);
            _toggleHotkeyService.Pressed +=
                (hotkeySender, hotkeyArgs) => ToggleOutput();
            _textReplacementHotkeyService.Pressed +=
                (hotkeySender, hotkeyArgs) => TranslateSelectedTextAsync();
            RegisterHotkeys();
        }

        private void ConfigureTray()
        {
            _trayIcon = new Forms.NotifyIcon
            {
                Icon = _appIcon ?? SystemIcons.Application,
                Text = "游戏翻译器",
                Visible = true
            };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("打开设置", null, (sender, args) => ShowMainWindow());
            menu.Items.Add("立即截图", null, (sender, args) =>
                Dispatcher.BeginInvoke(new Action(() => StartCapture(false))));
            menu.Items.Add("显示 / 隐藏输出框", null, (sender, args) =>
                Dispatcher.BeginInvoke(new Action(ToggleOutput)));
            menu.Items.Add("调整输出框位置", null, (sender, args) =>
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    EnsureOverlay();
                    _overlay.EnterPositionEditMode();
                })));
            menu.Items.Add("-");
            menu.Items.Add("退出", null, (sender, args) =>
                Dispatcher.BeginInvoke(new Action(ExitApplication)));
            _trayIcon.ContextMenuStrip = menu;
            _trayIcon.DoubleClick += (sender, args) =>
                Dispatcher.BeginInvoke(new Action(ShowMainWindow));
        }

        private void ShowMainWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs args)
        {
            if (_recordingHotkey == HotkeyRecordingTarget.None)
            {
                return;
            }

            var key = args.Key == Key.System ? args.SystemKey : args.Key;
            if (key == Key.LeftAlt || key == Key.RightAlt
                || key == Key.LeftCtrl || key == Key.RightCtrl
                || key == Key.LeftShift || key == Key.RightShift
                || key == Key.LWin || key == Key.RWin)
            {
                args.Handled = true;
                return;
            }

            if (key == Key.F12)
            {
                SetStatus("F12 是系统保留键，请选择其他按键。", true);
                _recordingHotkey = HotkeyRecordingTarget.None;
                RestoreHotkeyButtons();
                args.Handled = true;
                return;
            }

            var hotkey = FormatHotkey(Keyboard.Modifiers, key);
            if (string.IsNullOrWhiteSpace(hotkey))
            {
                return;
            }

            var isCapture = _recordingHotkey == HotkeyRecordingTarget.Capture;
            var otherHotkey = isCapture
                ? _settings.ToggleOverlayHotkey
                : _settings.Hotkey;
            if (hotkey.Equals(
                    otherHotkey,
                    StringComparison.OrdinalIgnoreCase)
                || hotkey.Equals(
                    _settings.TextReplacementHotkey,
                    StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("三个功能不能使用同一个快捷键。", true);
                _recordingHotkey = HotkeyRecordingTarget.None;
                RestoreHotkeyButtons();
                args.Handled = true;
                return;
            }

            if (isCapture)
            {
                _settings.Hotkey = hotkey;
            }
            else
            {
                _settings.ToggleOverlayHotkey = hotkey;
            }
            _recordingHotkey = HotkeyRecordingTarget.None;
            RestoreHotkeyButtons();
            _footerStatus.Text = "快捷键已修改，点击“保存”后生效";
            args.Handled = true;
        }

        private void BeginHotkeyRecording(HotkeyRecordingTarget target)
        {
            _recordingHotkey = target;
            RestoreHotkeyButtons();
            var button = target == HotkeyRecordingTarget.Capture
                ? _hotkeyButton
                : _toggleHotkeyButton;
            button.Content = "请按下按键…";
            _footerStatus.Text = target == HotkeyRecordingTarget.Capture
                ? "请按下新的截图快捷键"
                : "请按下新的输出框开关键";
            Focus();
        }

        private void RestoreHotkeyButtons()
        {
            _hotkeyButton.Content = _settings.Hotkey
                .ToUpperInvariant()
                .Replace("+", " + ");
            _toggleHotkeyButton.Content = _settings.ToggleOverlayHotkey
                .ToUpperInvariant()
                .Replace("+", " + ");
        }

        private static string FormatHotkey(ModifierKeys modifiers, Key key)
        {
            var text = "";
            if ((modifiers & ModifierKeys.Control) != 0) text += "Ctrl+";
            if ((modifiers & ModifierKeys.Alt) != 0) text += "Alt+";
            if ((modifiers & ModifierKeys.Shift) != 0) text += "Shift+";
            if ((modifiers & ModifierKeys.Windows) != 0) text += "Win+";
            text += new KeyConverter().ConvertToString(key);
            return text;
        }

        private void ApplyLiveOutputSettings()
        {
            if (_initializing)
            {
                return;
            }
            _settings.FontSize = _fontSizeSlider.Value;
            _settings.BackgroundOpacity = _opacitySlider.Value / 100.0;
            if (_overlay != null)
            {
                _overlay.ApplySettings(_settings);
            }
            _footerStatus.Text = "输出设置已修改，点击“保存”后生效";
        }

        private void SaveSettings(bool showStatus)
        {
            _settings.FontSize = _fontSizeSlider.Value;
            _settings.BackgroundOpacity = _opacitySlider.Value / 100.0;

            if (!RegisterHotkeys())
            {
                RestoreSettingsFromRegisteredHotkeys();
                return;
            }
            if (!TrySaveSettings())
            {
                return;
            }
            if (_overlay != null)
            {
                _overlay.ApplySettings(_settings);
            }
            if (showStatus)
            {
                SetStatus("设置已保存，截图键：" + _settings.Hotkey, false);
            }
        }

        private bool RegisterHotkeys()
        {
            if (_hotkeyService == null
                || _toggleHotkeyService == null
                || _textReplacementHotkeyService == null)
            {
                return true;
            }
            if (HotkeysEqual(
                    _settings.Hotkey,
                    _settings.ToggleOverlayHotkey)
                || HotkeysEqual(
                    _settings.Hotkey,
                    _settings.TextReplacementHotkey)
                || HotkeysEqual(
                    _settings.ToggleOverlayHotkey,
                    _settings.TextReplacementHotkey))
            {
                SetStatus("三个全局快捷键不能相同。", true);
                return false;
            }

            var previousCapture = _hotkeyService.RegisteredHotkey;
            var previousToggle = _toggleHotkeyService.RegisteredHotkey;
            var previousText =
                _textReplacementHotkeyService.RegisteredHotkey;
            string error;
            if (!_hotkeyService.Register(_settings.Hotkey, out error))
            {
                SetStatus("截图键：" + error, true);
                RestoreRegisteredHotkeys(
                    previousCapture,
                    previousToggle,
                    previousText);
                return false;
            }
            if (!_toggleHotkeyService.Register(
                _settings.ToggleOverlayHotkey,
                out error))
            {
                SetStatus("输出框开关键：" + error, true);
                RestoreRegisteredHotkeys(
                    previousCapture,
                    previousToggle,
                    previousText);
                return false;
            }
            if (!_textReplacementHotkeyService.Register(
                _settings.TextReplacementHotkey,
                out error))
            {
                SetStatus("文本替换快捷键：" + error, true);
                RestoreRegisteredHotkeys(
                    previousCapture,
                    previousToggle,
                    previousText);
                return false;
            }
            return true;
        }

        private void RestoreRegisteredHotkeys(
            string capture,
            string toggle,
            string textReplacement)
        {
            RestoreRegisteredHotkey(_hotkeyService, capture);
            RestoreRegisteredHotkey(_toggleHotkeyService, toggle);
            RestoreRegisteredHotkey(
                _textReplacementHotkeyService,
                textReplacement);
        }

        private static void RestoreRegisteredHotkey(
            HotkeyService service,
            string hotkey)
        {
            if (service == null)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(hotkey))
            {
                service.Unregister();
                return;
            }

            string ignored;
            service.Register(hotkey, out ignored);
        }

        private void RestoreSettingsFromRegisteredHotkeys()
        {
            if (_hotkeyService != null
                && !string.IsNullOrWhiteSpace(
                    _hotkeyService.RegisteredHotkey))
            {
                _settings.Hotkey =
                    _hotkeyService.RegisteredHotkey;
            }
            if (_toggleHotkeyService != null
                && !string.IsNullOrWhiteSpace(
                    _toggleHotkeyService.RegisteredHotkey))
            {
                _settings.ToggleOverlayHotkey =
                    _toggleHotkeyService.RegisteredHotkey;
            }
            if (_textReplacementHotkeyService != null
                && !string.IsNullOrWhiteSpace(
                    _textReplacementHotkeyService.RegisteredHotkey))
            {
                _settings.TextReplacementHotkey =
                    _textReplacementHotkeyService.RegisteredHotkey;
            }
            RestoreHotkeyButtons();
            UpdateTextReplacementSummary();
        }

        private static bool HotkeysEqual(string left, string right)
        {
            return string.Equals(
                left,
                right,
                StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureOverlay()
        {
            if (_overlay != null)
            {
                _overlay.ApplySettings(_settings);
                return;
            }
            _overlay = new OverlayWindow(_settings);
            _overlay.PositionSaved += (sender, args) =>
            {
                if (TrySaveSettings())
                {
                    _footerStatus.Text = "输出框位置已保存";
                }
            };
        }

        private bool TrySaveSettings()
        {
            try
            {
                _settingsStore.Save(_settings);
                return true;
            }
            catch (Exception ex)
            {
                SetStatus("保存设置失败：" + ex.Message, true);
                return false;
            }
        }

        private void ToggleOutput()
        {
            EnsureOverlay();
            var enabled = _overlay.ToggleOutputEnabled();
            SetStatus(enabled ? "输出框已开启。" : "输出框已关闭。", false);
        }

        private async void StartCapture(bool restoreSettingsWindow)
        {
            if (_captureWindow != null)
            {
                return;
            }

            SaveSettings(false);
            var wasVisible = restoreSettingsWindow && IsVisible;
            if (IsVisible)
            {
                Hide();
            }

            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            try
            {
                _captureWindow = new CaptureWindow();
                _captureWindow.RegionSelected += bitmap => ProcessSelectionAsync(bitmap);
                _captureWindow.Closed += (sender, args) =>
                {
                    _captureWindow = null;
                    if (wasVisible)
                    {
                        ShowMainWindow();
                    }
                };
                _captureWindow.Show();
                _captureWindow.Activate();
            }
            catch (Exception ex)
            {
                _captureWindow = null;
                if (wasVisible)
                {
                    ShowMainWindow();
                }
                SetStatus("无法启动截图：" + ex.Message, true);
            }
        }

        private async void ProcessSelectionAsync(Bitmap bitmap)
        {
            if (_workCancellation != null)
            {
                _workCancellation.Cancel();
            }
            var currentWork = new CancellationTokenSource();
            _workCancellation = currentWork;
            var token = currentWork.Token;
            EnsureOverlay();
            _overlay.SetWorking("文字识别");

            try
            {
                OcrResult ocr;
                using (bitmap)
                {
                    ocr = await _ocrEngine.RecognizeAsync(bitmap, token);
                }
                if (string.IsNullOrWhiteSpace(ocr.Text))
                {
                    throw new InvalidOperationException("没有识别到文字，请框选更清晰、更紧凑的字幕区域。");
                }

                _overlay.SetWorking("翻译");
                var provider = TranslationProviderFactory.Create(
                    _settings.TranslationProvider);
                var translated = await TranslationOperation.RunAsync(
                    provider,
                    new TranslationRequest
                    {
                        Text = ocr.Text,
                        SourceLanguage = "auto",
                        TargetLanguage = "zh-Hans"
                    },
                    _settings,
                    token);

                _overlay.SetResult(
                    translated.Text,
                    translated.Provider,
                    ocr.Duration + translated.Duration);
                SetStatus(
                    "翻译完成 · 识别 "
                    + ocr.Duration.TotalSeconds.ToString("0.00")
                    + " 秒 · 翻译 "
                    + translated.Duration.TotalSeconds.ToString("0.00") + " 秒",
                    false);
            }
            catch (TranslationTimeoutException ex)
            {
                _overlay.SetError(ex.Message);
                SetStatus(ex.Message, true);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _overlay.SetError(ex.Message);
                SetStatus(ex.Message, true);
            }
            finally
            {
                if (ReferenceEquals(
                    _workCancellation,
                    currentWork))
                {
                    _workCancellation = null;
                }
                currentWork.Dispose();
            }
        }

        private async void TranslateSelectedTextAsync()
        {
            if (_textReplacementBusy)
            {
                ShowTrayMessage(
                    "正在翻译",
                    "上一次文本翻译尚未完成，请稍候。",
                    Forms.ToolTipIcon.Info);
                return;
            }

            _textReplacementBusy = true;
            var currentReplacement = new CancellationTokenSource();
            _textReplacementCancellation = currentReplacement;
            var token = currentReplacement.Token;
            var originalCopiedAndDeleteSent = false;
            string originalText = null;
            var timedOut = false;
            var canceled = false;
            Exception failure = null;

            try
            {
                try
                {
                    SetStatus("正在读取选中文本…", false);
                    originalText =
                        await TextSelectionService.CopyAndDeleteAsync(token);
                    originalCopiedAndDeleteSent = true;
                    SetStatus("原文已复制，正在翻译…", false);

                    var provider = TranslationProviderFactory.Create(
                        _settings.TranslationProvider);
                    var translated = await TranslationOperation.RunAsync(
                        provider,
                        new TranslationRequest
                        {
                            Text = originalText,
                            SourceLanguage = "auto",
                            TargetLanguage =
                                _settings.TextReplacementTargetLanguage
                        },
                        _settings,
                        token);
                    await TextSelectionService.SetClipboardTextAsync(
                        translated.Text,
                        token);

                    var targetName = TranslationLanguages.GetDisplayName(
                        _settings.TextReplacementTargetLanguage);
                    SetStatus(
                        "文本已翻译为" + targetName + "并写入剪贴板。",
                        false);
                    ShowTrayMessage(
                        "翻译完成",
                        "译文已复制，可按 Ctrl+V 粘贴。",
                        Forms.ToolTipIcon.Info);
                }
                catch (TranslationTimeoutException)
                {
                    timedOut = true;
                }
                catch (OperationCanceledException)
                {
                    canceled = true;
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                if (timedOut)
                {
                    const string message =
                        "翻译超时（10 秒），本次操作已取消。";
                    var recovery =
                        await RestoreOriginalClipboardAsync(
                            originalText,
                            originalCopiedAndDeleteSent);
                    SetStatus(message, true);
                    ShowTrayMessage(
                        "文本翻译超时",
                        message + " " + recovery,
                        Forms.ToolTipIcon.Error);
                }
                else if (canceled)
                {
                    await RestoreOriginalClipboardAsync(
                        originalText,
                        originalCopiedAndDeleteSent);
                }
                else if (failure != null)
                {
                    var recovery =
                        await RestoreOriginalClipboardAsync(
                            originalText,
                            originalCopiedAndDeleteSent);
                    SetStatus(failure.Message, true);
                    ShowTrayMessage(
                        "文本翻译失败",
                        failure.Message
                        + (originalCopiedAndDeleteSent
                            ? " " + recovery
                            : " 没有删除任何文本。"),
                        Forms.ToolTipIcon.Error);
                }
            }
            finally
            {
                if (ReferenceEquals(
                    _textReplacementCancellation,
                    currentReplacement))
                {
                    _textReplacementCancellation = null;
                }
                currentReplacement.Dispose();
                _textReplacementBusy = false;
            }
        }

        private static async Task<string> RestoreOriginalClipboardAsync(
            string originalText,
            bool shouldRestore)
        {
            if (!shouldRestore || string.IsNullOrEmpty(originalText))
            {
                return "";
            }

            using (var recoveryCancellation =
                new CancellationTokenSource(1200))
            {
                try
                {
                    await TextSelectionService.SetClipboardTextAsync(
                        originalText,
                        recoveryCancellation.Token);
                    return "原文已恢复到剪贴板，可按 Ctrl+V 粘贴。";
                }
                catch
                {
                    return "剪贴板被占用，未能自动恢复原文。";
                }
            }
        }

        private void ShowTrayMessage(
            string title,
            string message,
            Forms.ToolTipIcon icon)
        {
            if (_trayIcon == null)
            {
                return;
            }
            _trayIcon.ShowBalloonTip(2500, title, message, icon);
        }

        private void SetStatus(string text, bool error)
        {
            _headerStatus.Text = error ? "发生错误" : "系统就绪";
            _footerStatus.Text = (error ? "错误 · " : "就绪 · ") + text;
        }

        private T Find<T>(string name) where T : class
        {
            var value = _root.FindName(name) as T;
            if (value == null)
            {
                throw new InvalidOperationException("界面缺少控件：" + name);
            }
            return value;
        }

        private void OnClosing(object sender, CancelEventArgs args)
        {
            if (_exiting)
            {
                return;
            }

            args.Cancel = true;

            var choiceWindow = new CloseChoiceWindow(this);
            var choice = choiceWindow.ShowChoice();
            if (choice == CloseChoice.Exit)
            {
                Dispatcher.BeginInvoke(new Action(ExitApplication));
                return;
            }
            if (choice != CloseChoice.MinimizeToTray)
            {
                return;
            }

            Hide();
            if (_trayIcon != null)
            {
                _trayIcon.ShowBalloonTip(
                    1200,
                    "游戏翻译器",
                    "程序仍在后台运行，可使用截图快捷键。",
                    Forms.ToolTipIcon.Info);
            }
        }

        private void ExitApplication()
        {
            _exiting = true;
            SaveSettings(false);
            if (_workCancellation != null)
            {
                _workCancellation.Cancel();
                _workCancellation = null;
            }
            if (_textReplacementCancellation != null)
            {
                _textReplacementCancellation.Cancel();
                _textReplacementCancellation = null;
            }
            if (_hotkeyService != null)
            {
                _hotkeyService.Dispose();
            }
            if (_toggleHotkeyService != null)
            {
                _toggleHotkeyService.Dispose();
            }
            if (_textReplacementHotkeyService != null)
            {
                _textReplacementHotkeyService.Dispose();
            }
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            if (_appIcon != null)
            {
                _appIcon.Dispose();
            }
            if (_overlay != null)
            {
                _overlay.Close();
            }
            Close();
            Application.Current.Shutdown();
        }
    }
}
