using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace GameTranslator
{
    public enum CloseChoice
    {
        Cancel,
        MinimizeToTray,
        Exit
    }

    public sealed class CloseChoiceWindow : Window
    {
        private static readonly Brush BackgroundBrush =
            new SolidColorBrush(Color.FromRgb(243, 240, 232));
        private static readonly Brush InkBrush =
            new SolidColorBrush(Color.FromRgb(21, 21, 21));
        private static readonly Brush OrangeBrush =
            new SolidColorBrush(Color.FromRgb(255, 106, 0));

        public CloseChoice Choice { get; private set; }

        public CloseChoiceWindow(Window owner)
        {
            Owner = owner;
            Title = "关闭游戏翻译器";
            Width = 390;
            Height = 210;
            MinWidth = 390;
            MinHeight = 210;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = BackgroundBrush;
            Icon = owner.Icon;
            Choice = CloseChoice.Cancel;

            Content = BuildContent();
            PreviewKeyDown += OnPreviewKeyDown;
        }

        public CloseChoice ShowChoice()
        {
            ShowDialog();
            return Choice;
        }

        private FrameworkElement BuildContent()
        {
            var root = new Grid
            {
                Margin = new Thickness(22, 18, 22, 18)
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var title = new TextBlock
            {
                Text = "关闭窗口后要做什么？",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 19,
                FontWeight = FontWeights.Bold,
                Foreground = InkBrush
            };
            root.Children.Add(title);

            var hint = new TextBlock
            {
                Text = "最小化后，截图快捷键仍可继续使用。",
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(103, 100, 95)),
                Margin = new Thickness(0, 6, 0, 16)
            };
            Grid.SetRow(hint, 1);
            root.Children.Add(hint);

            var actions = new Grid();
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            actions.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetRow(actions, 2);

            var minimizeButton = CreateButton(
                "最小化到托盘",
                OrangeBrush,
                Brushes.White,
                OrangeBrush);
            minimizeButton.Click += (sender, args) =>
            {
                Choice = CloseChoice.MinimizeToTray;
                DialogResult = true;
            };
            actions.Children.Add(minimizeButton);

            var exitButton = CreateButton(
                "关闭程序",
                BackgroundBrush,
                InkBrush,
                InkBrush);
            exitButton.Click += (sender, args) =>
            {
                Choice = CloseChoice.Exit;
                DialogResult = true;
            };
            Grid.SetColumn(exitButton, 2);
            actions.Children.Add(exitButton);

            root.Children.Add(actions);
            return root;
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
                Height = 42,
                Background = background,
                Foreground = foreground,
                BorderBrush = border,
                BorderThickness = new Thickness(1),
                FontFamily = new FontFamily("Microsoft YaHei UI"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand
            };
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key == Key.Escape)
            {
                Choice = CloseChoice.Cancel;
                Close();
                args.Handled = true;
            }
        }
    }
}
