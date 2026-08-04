using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace GameTranslatorLauncher
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            var projectRoot = AppDomain.CurrentDomain.BaseDirectory;
            var executable = Path.Combine(projectRoot, "dist", "GameTranslator.exe");

            if (!File.Exists(executable))
            {
                MessageBox.Show(
                    "找不到 dist\\GameTranslator.exe，请确认程序文件完整。",
                    "游戏翻译器",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    WorkingDirectory = Path.GetDirectoryName(executable),
                    UseShellExecute = true
                });
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    "无法启动游戏翻译器：\n" + exception.Message,
                    "游戏翻译器",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
