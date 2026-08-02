using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GameTranslator
{
    public sealed class TranslationSettingsWindow : Window
    {
        private static readonly Brush Paper =
            new SolidColorBrush(Color.FromRgb(243, 240, 232));
        private static readonly Brush Ink =
            new SolidColorBrush(Color.FromRgb(21, 21, 21));
        private static readonly Brush Orange =
            new SolidColorBrush(Color.FromRgb(255, 106, 0));
        private static readonly Brush Muted =
            new SolidColorBrush(Color.FromRgb(103, 100, 95));
        private static readonly Brush Line =
            new SolidColorBrush(Color.FromRgb(201, 197, 188));

        private readonly AppSettings _settings;
        private readonly Dictionary<string, RadioButton> _providerButtons =
            new Dictionary<string, RadioButton>();
        private readonly Dictionary<string, Border> _providerCards =
            new Dictionary<string, Border>();

        private PasswordBox _deepLKey;
        private CheckBox _deepLFreeApi;
        private PasswordBox _microsoftKey;
        private TextBox _microsoftRegion;
        private TextBox _microsoftEndpoint;
        private TextBox _lingvaEndpoint;
        private TextBox _libreNoKeyEndpoint;
        private TextBox _libreApiEndpoint;
        private PasswordBox _libreKey;

        public bool SettingsSaved { get; private set; }

        public TranslationSettingsWindow(Window owner, AppSettings settings)
        {
            Owner = owner;
            _settings = settings;
            Title = "翻译接口";
            Width = 650;
            Height = 660;
            MinWidth = 570;
            MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Paper;
            Icon = owner.Icon;
            ShowInTaskbar = false;

            Content = BuildContent();
            SelectProvider(settings.TranslationProvider);
            PreviewKeyDown += (sender, args) =>
            {
                if (args.Key == Key.Escape)
                {
                    Close();
                    args.Handled = true;
                }
            };
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
                Margin = new Thickness(24, 20, 24, 16)
            };
            header.Children.Add(new TextBlock
            {
                Text = "翻译接口",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 23,
                FontWeight = FontWeights.Bold,
                Foreground = Ink
            });
            header.Children.Add(new TextBlock
            {
                Text = "每组按质量、速度与稳定性综合排序 · 选择一个截图翻译接口",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 11,
                Foreground = Muted,
                Margin = new Thickness(0, 5, 0, 0)
            });
            root.Children.Add(header);

            var options = new StackPanel
            {
                Margin = new Thickness(24, 0, 24, 18)
            };
            options.Children.Add(CreateGroupHeader(
                "无需 API 密钥",
                "组内综合性能从高到低 · 开箱即用或使用公共节点"));
            options.Children.Add(CreateAutomaticCard());
            options.Children.Add(CreateGoogleCard());
            options.Children.Add(CreateLingvaCard());
            options.Children.Add(CreateMyMemoryCard());
            options.Children.Add(CreateLibreNoKeyCard());
            options.Children.Add(CreateGroupHeader(
                "需要 API 密钥",
                "组内综合性能从高到低 · 需自行申请对应服务"));
            options.Children.Add(CreateMicrosoftCard());
            options.Children.Add(CreateDeepLCard());
            options.Children.Add(CreateLibreApiCard());

            var scroll = new ScrollViewer
            {
                Content = options,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            var footer = new Grid
            {
                Margin = new Thickness(24, 14, 24, 18)
            };
            footer.ColumnDefinitions.Add(new ColumnDefinition());
            footer.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto
            });
            footer.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(10)
            });
            footer.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto
            });
            footer.Children.Add(new TextBlock
            {
                Text = "内置游戏词典无需联网 · API 密钥仅保存在本机",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 10,
                Foreground = Muted,
                VerticalAlignment = VerticalAlignment.Center
            });

            var cancel = CreateButton("取消", Paper, Ink, Ink, 86);
            cancel.Click += (sender, args) => Close();
            Grid.SetColumn(cancel, 1);
            footer.Children.Add(cancel);

            var save = CreateButton(
                "保存并使用",
                Orange,
                Brushes.White,
                Orange,
                112);
            save.Click += OnSaveClicked;
            Grid.SetColumn(save, 3);
            footer.Children.Add(save);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            return root;
        }

        private Border CreateDeepLCard()
        {
            _deepLKey = CreatePasswordBox(_settings.DeepLApiKey);
            _deepLFreeApi = new CheckBox
            {
                Content = "使用 DeepL API Free 地址",
                IsChecked = _settings.DeepLUseFreeApi,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 10,
                Foreground = Ink,
                Margin = new Thickness(0, 7, 0, 0)
            };
            var fields = new StackPanel();
            fields.Children.Add(CreateFieldLabel("API 密钥"));
            fields.Children.Add(_deepLKey);
            fields.Children.Add(_deepLFreeApi);
            return CreateProviderCard(
                "02",
                "DeepL API",
                "质量优先 · 需要 DeepL API 密钥",
                fields);
        }

        private Border CreateMicrosoftCard()
        {
            _microsoftKey = CreatePasswordBox(
                _settings.MicrosoftTranslatorKey);
            _microsoftRegion = CreateTextBox(
                _settings.MicrosoftTranslatorRegion,
                "例如：eastasia（部分资源可留空）");
            _microsoftEndpoint = CreateTextBox(
                _settings.MicrosoftTranslatorEndpoint,
                "全球：https://api.cognitive.microsofttranslator.com；中国区：https://api.translator.azure.cn");
            var fields = new StackPanel();
            fields.Children.Add(CreateFieldLabel("服务地址"));
            fields.Children.Add(_microsoftEndpoint);
            fields.Children.Add(CreateFieldLabel("订阅密钥"));
            fields.Children.Add(_microsoftKey);
            fields.Children.Add(CreateFieldLabel("区域"));
            fields.Children.Add(_microsoftRegion);
            return CreateProviderCard(
                "01",
                "Microsoft Translator",
                "国内通常可直连 · 支持全球、中国区和自定义端点",
                fields);
        }

        private Border CreateAutomaticCard()
        {
            return CreateProviderCard(
                "01",
                "智能在线翻译",
                "本地游戏词典优先 · Google 优先 · MyMemory 延迟备用",
                null);
        }

        private Border CreateGoogleCard()
        {
            return CreateProviderCard(
                "02",
                "Google 在线翻译",
                "Google 网页翻译接口直连 · 无需密钥",
                null);
        }

        private Border CreateLingvaCard()
        {
            _lingvaEndpoint = CreateTextBox(
                _settings.LingvaEndpoint,
                "例如：https://lingva.lunar.icu");
            var fields = new StackPanel();
            fields.Children.Add(CreateFieldLabel("公共或自建节点"));
            fields.Children.Add(_lingvaEndpoint);
            return CreateProviderCard(
                "03",
                "Lingva Translate",
                "开源 Google 翻译前端 · 节点无需密钥",
                fields);
        }

        private Border CreateLibreNoKeyCard()
        {
            _libreNoKeyEndpoint = CreateTextBox(
                _settings.LibreTranslateEndpoint,
                "例如：https://translate.argosopentech.com");
            var fields = new StackPanel();
            fields.Children.Add(CreateFieldLabel("免密钥公共或自建节点"));
            fields.Children.Add(_libreNoKeyEndpoint);
            return CreateProviderCard(
                "05",
                "LibreTranslate 免密钥",
                "开源翻译服务 · 公共节点稳定性可能变化",
                fields);
        }

        private Border CreateMyMemoryCard()
        {
            return CreateProviderCard(
                "04",
                "MyMemory 免费翻译",
                "免费直连 · 无需密钥 · 有公共服务额度限制",
                null);
        }

        private Border CreateLibreApiCard()
        {
            _libreApiEndpoint = CreateTextBox(
                _settings.LibreTranslateEndpoint,
                "例如：https://libretranslate.com");
            _libreKey = CreatePasswordBox(_settings.LibreTranslateApiKey);
            var fields = new StackPanel();
            fields.Children.Add(CreateFieldLabel("服务地址"));
            fields.Children.Add(_libreApiEndpoint);
            fields.Children.Add(CreateFieldLabel("API 密钥"));
            fields.Children.Add(_libreKey);
            return CreateProviderCard(
                "03",
                "LibreTranslate API",
                "带密钥的托管或私有服务",
                fields);
        }

        private FrameworkElement CreateGroupHeader(
            string title,
            string description)
        {
            var header = new StackPanel
            {
                Margin = new Thickness(0, 8, 0, 9)
            };
            header.Children.Add(new TextBlock
            {
                Text = title,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Ink
            });
            header.Children.Add(new TextBlock
            {
                Text = description,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 10,
                Foreground = Muted,
                Margin = new Thickness(0, 3, 0, 0)
            });
            return header;
        }

        private Border CreateProviderCard(
            string rank,
            string provider,
            string description,
            FrameworkElement fields)
        {
            var card = new Border
            {
                BorderBrush = Line,
                BorderThickness = new Thickness(1),
                Background = Paper,
                Padding = new Thickness(14, 11, 14, 12),
                Margin = new Thickness(0, 0, 0, 9)
            };
            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(42)
            });
            layout.ColumnDefinitions.Add(new ColumnDefinition());

            var rankBlock = new TextBlock
            {
                Text = rank,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = Orange,
                Margin = new Thickness(0, 3, 0, 0)
            };
            layout.Children.Add(rankBlock);

            var content = new StackPanel();
            var radio = new RadioButton
            {
                Content = provider,
                GroupName = "TranslationProvider",
                Tag = provider,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Ink
            };
            radio.Checked += (sender, args) => UpdateCardAppearance();
            content.Children.Add(radio);
            content.Children.Add(new TextBlock
            {
                Text = description,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 10,
                Foreground = Muted,
                Margin = new Thickness(22, 3, 0, fields == null ? 0 : 8)
            });
            if (fields != null)
            {
                fields.Margin = new Thickness(22, 0, 0, 0);
                content.Children.Add(fields);
            }
            Grid.SetColumn(content, 1);
            layout.Children.Add(content);
            card.Child = layout;

            card.MouseLeftButtonDown += (sender, args) =>
            {
                radio.IsChecked = true;
            };
            _providerButtons[provider] = radio;
            _providerCards[provider] = card;
            return card;
        }

        private void SelectProvider(string provider)
        {
            RadioButton radio;
            if (!_providerButtons.TryGetValue(provider ?? "", out radio))
            {
                radio = _providerButtons["智能在线翻译"];
            }
            radio.IsChecked = true;
            UpdateCardAppearance();
        }

        private string GetSelectedProvider()
        {
            foreach (var item in _providerButtons)
            {
                if (item.Value.IsChecked == true)
                {
                    return item.Key;
                }
            }
            return "智能在线翻译";
        }

        private void UpdateCardAppearance()
        {
            foreach (var item in _providerCards)
            {
                var selected = _providerButtons[item.Key].IsChecked == true;
                item.Value.BorderBrush = selected ? Orange : Line;
                item.Value.BorderThickness = new Thickness(selected ? 2 : 1);
            }
        }

        private void OnSaveClicked(object sender, RoutedEventArgs args)
        {
            var provider = GetSelectedProvider();
            if (provider == "DeepL API"
                && string.IsNullOrWhiteSpace(_deepLKey.Password))
            {
                ShowValidation("请填写 DeepL API 密钥。");
                return;
            }
            if (provider == "Microsoft Translator"
                && string.IsNullOrWhiteSpace(_microsoftKey.Password))
            {
                ShowValidation("请填写 Microsoft Translator 订阅密钥。");
                return;
            }
            if ((provider == "Microsoft Translator"
                    || (provider == "智能在线翻译"
                        && !string.IsNullOrWhiteSpace(
                            _microsoftKey.Password)))
                && !IsValidSecureEndpoint(_microsoftEndpoint.Text))
            {
                ShowValidation(
                    "请填写有效的 Microsoft Translator HTTPS 服务地址。");
                return;
            }
            if (provider == "LibreTranslate API"
                && string.IsNullOrWhiteSpace(_libreKey.Password))
            {
                ShowValidation("请填写 LibreTranslate API 密钥。");
                return;
            }
            if (provider == "Lingva Translate"
                && !IsValidEndpoint(_lingvaEndpoint.Text))
            {
                ShowValidation("请填写有效的 Lingva 节点地址。");
                return;
            }
            var libreEndpoint = provider == "LibreTranslate API"
                ? _libreApiEndpoint.Text.Trim()
                : _libreNoKeyEndpoint.Text.Trim();
            if ((provider == "LibreTranslate 免密钥"
                || provider == "LibreTranslate API")
                && !IsValidEndpoint(libreEndpoint))
            {
                ShowValidation(
                    "请填写有效的 LibreTranslate 服务地址。");
                return;
            }

            _settings.TranslationProvider = provider;
            _settings.DeepLApiKey = _deepLKey.Password.Trim();
            _settings.DeepLUseFreeApi = _deepLFreeApi.IsChecked == true;
            _settings.MicrosoftTranslatorKey =
                _microsoftKey.Password.Trim();
            _settings.MicrosoftTranslatorRegion =
                _microsoftRegion.Text.Trim();
            _settings.MicrosoftTranslatorEndpoint =
                _microsoftEndpoint.Text.Trim();
            _settings.LingvaEndpoint = _lingvaEndpoint.Text.Trim();
            if (provider == "LibreTranslate 免密钥"
                || provider == "LibreTranslate API")
            {
                _settings.LibreTranslateEndpoint = libreEndpoint;
            }
            _settings.LibreTranslateApiKey = _libreKey.Password.Trim();
            SettingsSaved = true;
            DialogResult = true;
        }

        private static bool IsValidEndpoint(string value)
        {
            Uri endpoint;
            return Uri.TryCreate(
                    (value ?? "").Trim(),
                    UriKind.Absolute,
                    out endpoint)
                && (endpoint.Scheme == Uri.UriSchemeHttp
                    || endpoint.Scheme == Uri.UriSchemeHttps);
        }

        private static bool IsValidSecureEndpoint(string value)
        {
            Uri endpoint;
            return Uri.TryCreate(
                    (value ?? "").Trim(),
                    UriKind.Absolute,
                    out endpoint)
                && endpoint.Scheme == Uri.UriSchemeHttps;
        }

        private void ShowValidation(string message)
        {
            MessageBox.Show(
                this,
                message,
                "翻译接口",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private static TextBlock CreateFieldLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = Ink,
                Margin = new Thickness(0, 5, 0, 3)
            };
        }

        private static TextBox CreateTextBox(
            string value,
            string toolTip)
        {
            return new TextBox
            {
                Text = value ?? "",
                ToolTip = toolTip,
                Height = 30,
                Padding = new Thickness(8, 5, 8, 5),
                Background = Paper,
                Foreground = Ink,
                BorderBrush = Ink,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11
            };
        }

        private static PasswordBox CreatePasswordBox(string value)
        {
            var box = new PasswordBox
            {
                Height = 30,
                Padding = new Thickness(8, 5, 8, 5),
                Background = Paper,
                Foreground = Ink,
                BorderBrush = Ink,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11
            };
            box.Password = value ?? "";
            return box;
        }

        private static Button CreateButton(
            string text,
            Brush background,
            Brush foreground,
            Brush border,
            double width)
        {
            return new Button
            {
                Content = text,
                Width = width,
                Height = 38,
                Background = background,
                Foreground = foreground,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand
            };
        }
    }
}
