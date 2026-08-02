using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GameTranslator
{
    public sealed class TextReplacementSettingsWindow : Window
    {
        private static readonly Brush Paper =
            new SolidColorBrush(Color.FromRgb(243, 240, 232));
        private static readonly Brush Ink =
            new SolidColorBrush(Color.FromRgb(21, 21, 21));
        private static readonly Brush Orange =
            new SolidColorBrush(Color.FromRgb(255, 106, 0));
        private static readonly Brush Muted =
            new SolidColorBrush(Color.FromRgb(103, 100, 95));
        private static readonly Brush Red =
            new SolidColorBrush(Color.FromRgb(227, 59, 46));

        private readonly string _captureHotkey;
        private readonly string _toggleHotkey;
        private readonly Button _hotkeyButton;
        private readonly ComboBox _languageComboBox;
        private readonly TextBlock _status;
        private bool _recording;

        public bool SettingsSaved { get; private set; }
        public string SelectedHotkey { get; private set; }
        public string SelectedTargetLanguage { get; private set; }

        public TextReplacementSettingsWindow(
            Window owner,
            AppSettings settings)
        {
            Owner = owner;
            Title = "文本替换";
            Width = 430;
            Height = 350;
            MinWidth = 410;
            MinHeight = 330;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Background = Paper;
            Icon = owner.Icon;

            _captureHotkey = settings.Hotkey;
            _toggleHotkey = settings.ToggleOverlayHotkey;
            SelectedHotkey = settings.TextReplacementHotkey;
            SelectedTargetLanguage = TranslationLanguages.Normalize(
                settings.TextReplacementTargetLanguage);

            _hotkeyButton = CreateButton(
                FormatHotkeyForDisplay(SelectedHotkey),
                Paper,
                Ink,
                Ink);
            _hotkeyButton.HorizontalContentAlignment =
                HorizontalAlignment.Left;
            _hotkeyButton.Click += (sender, args) =>
            {
                _recording = true;
                _hotkeyButton.Content = "请按下按键…";
                _status.Text = "正在录制新的全局快捷键";
                _status.Foreground = Orange;
                Focus();
            };

            _languageComboBox = new ComboBox
            {
                ItemsSource = TranslationLanguages.GetOptions(),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 12,
                Background = Brushes.White,
                Foreground = Ink,
                BorderBrush = Ink,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6)
            };
            foreach (TranslationLanguageOption option
                in _languageComboBox.Items)
            {
                if (option.Code == SelectedTargetLanguage)
                {
                    _languageComboBox.SelectedItem = option;
                    break;
                }
            }

            _status = new TextBlock
            {
                Text = "选中文本后按快捷键，译文会写入剪贴板",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 10,
                Foreground = Muted,
                TextWrapping = TextWrapping.Wrap
            };

            Content = BuildContent();
            PreviewKeyDown += OnPreviewKeyDown;
        }

        private FrameworkElement BuildContent()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto
            });
            root.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });
            root.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto
            });

            var header = new StackPanel
            {
                Margin = new Thickness(22, 18, 22, 14)
            };
            header.Children.Add(new TextBlock
            {
                Text = "文本替换",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = Ink
            });
            header.Children.Add(new TextBlock
            {
                Text = "复制选区 · 删除原文 · 翻译到剪贴板",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 10,
                Foreground = Muted,
                Margin = new Thickness(0, 4, 0, 0)
            });
            root.Children.Add(header);

            var cardContent = new StackPanel();
            cardContent.Children.Add(CreateLabel("文本替换快捷键"));
            cardContent.Children.Add(_hotkeyButton);
            cardContent.Children.Add(CreateLabel("目标语言", 12));
            cardContent.Children.Add(_languageComboBox);
            cardContent.Children.Add(new TextBlock
            {
                Text = "选中的文本会发送到当前翻译接口。翻译失败时，原文仍保留在剪贴板中。",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 10,
                Foreground = Muted,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 0)
            });

            var card = new Border
            {
                Margin = new Thickness(22, 0, 22, 12),
                Padding = new Thickness(14),
                Background = Brushes.White,
                BorderBrush = Ink,
                BorderThickness = new Thickness(1),
                Child = cardContent
            };
            Grid.SetRow(card, 1);
            root.Children.Add(card);

            var footer = new Grid
            {
                Margin = new Thickness(22, 0, 22, 18)
            };
            footer.ColumnDefinitions.Add(new ColumnDefinition());
            footer.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto
            });
            footer.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(8)
            });
            footer.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto
            });
            footer.Children.Add(_status);

            var cancel = CreateButton("取消", Paper, Ink, Ink);
            cancel.Width = 76;
            cancel.Click += (sender, args) => Close();
            Grid.SetColumn(cancel, 1);
            footer.Children.Add(cancel);

            var save = CreateButton("保存", Orange, Brushes.White, Orange);
            save.Width = 88;
            save.Click += OnSaveClicked;
            Grid.SetColumn(save, 3);
            footer.Children.Add(save);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);
            return root;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs args)
        {
            if (!_recording)
            {
                if (args.Key == Key.Escape)
                {
                    Close();
                    args.Handled = true;
                }
                return;
            }

            var key = args.Key == Key.System ? args.SystemKey : args.Key;
            if (key == Key.Escape)
            {
                _recording = false;
                _hotkeyButton.Content =
                    FormatHotkeyForDisplay(SelectedHotkey);
                _status.Text = "已取消录制";
                _status.Foreground = Muted;
                args.Handled = true;
                return;
            }
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
                ShowError("F12 是系统保留键，请选择其他按键。");
                _recording = false;
                _hotkeyButton.Content =
                    FormatHotkeyForDisplay(SelectedHotkey);
                args.Handled = true;
                return;
            }

            var hotkey = FormatHotkey(Keyboard.Modifiers, key);
            if (string.IsNullOrWhiteSpace(hotkey))
            {
                return;
            }
            if (HotkeyRules.IsUnsafeTextReplacementHotkey(hotkey))
            {
                ShowError(
                    "Ctrl+C、Ctrl+V 和 Delete 会与复制恢复流程冲突，请换一个快捷键。");
                _recording = false;
                _hotkeyButton.Content =
                    FormatHotkeyForDisplay(SelectedHotkey);
                args.Handled = true;
                return;
            }
            if (HotkeysEqual(hotkey, _captureHotkey)
                || HotkeysEqual(hotkey, _toggleHotkey))
            {
                ShowError("该快捷键已被截图或输出框开关使用。");
                _recording = false;
                _hotkeyButton.Content =
                    FormatHotkeyForDisplay(SelectedHotkey);
                args.Handled = true;
                return;
            }

            SelectedHotkey = hotkey;
            _recording = false;
            _hotkeyButton.Content = FormatHotkeyForDisplay(hotkey);
            _status.Text = "快捷键已录制，点击“保存”后生效";
            _status.Foreground = Orange;
            args.Handled = true;
        }

        private void OnSaveClicked(object sender, RoutedEventArgs args)
        {
            var selected =
                _languageComboBox.SelectedItem as TranslationLanguageOption;
            if (selected == null)
            {
                ShowError("请选择目标语言。");
                return;
            }
            if (HotkeysEqual(SelectedHotkey, _captureHotkey)
                || HotkeysEqual(SelectedHotkey, _toggleHotkey))
            {
                ShowError("三个全局快捷键不能相同。");
                return;
            }
            if (HotkeyRules.IsUnsafeTextReplacementHotkey(
                SelectedHotkey))
            {
                ShowError(
                    "该快捷键会与复制或恢复流程冲突，请重新录制。");
                return;
            }

            SelectedTargetLanguage = selected.Code;
            SettingsSaved = true;
            DialogResult = true;
            Close();
        }

        private void ShowError(string message)
        {
            _status.Text = message;
            _status.Foreground = Red;
        }

        private static TextBlock CreateLabel(
            string text,
            double topMargin = 0)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Ink,
                Margin = new Thickness(0, topMargin, 0, 5)
            };
        }

        private static Button CreateButton(
            string text,
            Brush background,
            Brush foreground,
            Brush border)
        {
            return new Button
            {
                Content = text,
                Height = 34,
                Padding = new Thickness(10, 5, 10, 5),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Background = background,
                Foreground = foreground,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
        }

        private static string FormatHotkey(
            ModifierKeys modifiers,
            Key key)
        {
            var text = "";
            if ((modifiers & ModifierKeys.Control) != 0) text += "Ctrl+";
            if ((modifiers & ModifierKeys.Alt) != 0) text += "Alt+";
            if ((modifiers & ModifierKeys.Shift) != 0) text += "Shift+";
            if ((modifiers & ModifierKeys.Windows) != 0) text += "Win+";
            text += new KeyConverter().ConvertToString(key);
            return text;
        }

        private static string FormatHotkeyForDisplay(string hotkey)
        {
            return (hotkey ?? "F6").ToUpperInvariant().Replace("+", " + ");
        }

        private static bool HotkeysEqual(string left, string right)
        {
            return string.Equals(
                left,
                right,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
