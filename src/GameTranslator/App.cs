using System;
using System.Threading;
using System.Windows;

namespace GameTranslator
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (var instanceMutex = new Mutex(
                true,
                @"Local\GameTranslator.SingleInstance",
                out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "游戏翻译器已经在运行，请检查任务栏托盘。",
                        "游戏翻译器",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var app = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };

                app.DispatcherUnhandledException += (sender, args) =>
                {
                    MessageBox.Show(
                        "程序遇到未处理错误：\n" + args.Exception.Message,
                        "游戏翻译器",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    args.Handled = true;
                };

                try
                {
                    var window = new MainWindow();
                    app.Run(window);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "程序启动失败：\n" + ex.Message,
                        "游戏翻译器",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
    }
}
