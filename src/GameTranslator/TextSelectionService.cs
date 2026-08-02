using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace GameTranslator
{
    internal static class TextSelectionService
    {
        private const uint InputKeyboard = 1;
        private const ushort VirtualKeyControl = 0x11;
        private const ushort VirtualKeyC = 0x43;
        private const ushort VirtualKeyDelete = 0x2E;
        private const uint KeyEventKeyUp = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct Input
        {
            public uint Type;
            public InputUnion Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public KeyboardInput Keyboard;

            [FieldOffset(0)]
            public MouseInput Mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [DllImport("user32.dll")]
        private static extern uint SendInput(
            uint inputCount,
            Input[] inputs,
            int inputSize);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        public static async Task<string> CopyAndDeleteAsync(
            CancellationToken cancellationToken)
        {
            var foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero)
            {
                throw new InvalidOperationException("无法确定当前文本窗口。");
            }

            await Task.Delay(80, cancellationToken);
            if (GetForegroundWindow() != foreground)
            {
                throw new InvalidOperationException(
                    "当前窗口已经改变，没有删除任何文本。");
            }

            var clipboardSequence = GetClipboardSequenceNumber();
            if (!SendChord(VirtualKeyControl, VirtualKeyC))
            {
                throw new InvalidOperationException(
                    "无法复制选中文本，可能是目标程序权限较高。");
            }

            string selectedText = null;
            for (var attempt = 0; attempt < 12; attempt++)
            {
                await Task.Delay(100, cancellationToken);
                if (GetClipboardSequenceNumber() == clipboardSequence)
                {
                    continue;
                }
                selectedText = TryReadClipboardText();
                if (!string.IsNullOrWhiteSpace(selectedText))
                {
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(selectedText))
            {
                throw new InvalidOperationException(
                    "没有复制到选中文本，请先选中可编辑文字。");
            }
            if (GetForegroundWindow() != foreground)
            {
                throw new InvalidOperationException(
                    "复制后当前窗口发生了变化，没有删除任何文本。");
            }
            if (!SendKey(VirtualKeyDelete))
            {
                throw new InvalidOperationException(
                    "无法删除原文本；原文已保存在剪贴板。");
            }
            return selectedText;
        }

        public static async Task SetClipboardTextAsync(
            string text,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("翻译接口没有返回可复制的译文。");
            }

            Exception lastError = null;
            for (var attempt = 0; attempt < 10; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    Clipboard.SetText(text, TextDataFormat.UnicodeText);
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
                await Task.Delay(80, cancellationToken);
            }
            throw new InvalidOperationException(
                "剪贴板正被其他程序占用，原文仍保留在剪贴板中。",
                lastError);
        }

        private static string TryReadClipboardText()
        {
            try
            {
                return Clipboard.ContainsText(TextDataFormat.UnicodeText)
                    ? Clipboard.GetText(TextDataFormat.UnicodeText)
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool SendChord(ushort modifier, ushort key)
        {
            var inputs = new[]
            {
                CreateKeyboardInput(modifier, false),
                CreateKeyboardInput(key, false),
                CreateKeyboardInput(key, true),
                CreateKeyboardInput(modifier, true)
            };
            return SendInput(
                (uint)inputs.Length,
                inputs,
                Marshal.SizeOf(typeof(Input))) == (uint)inputs.Length;
        }

        private static bool SendKey(ushort key)
        {
            var inputs = new[]
            {
                CreateKeyboardInput(key, false),
                CreateKeyboardInput(key, true)
            };
            return SendInput(
                (uint)inputs.Length,
                inputs,
                Marshal.SizeOf(typeof(Input))) == (uint)inputs.Length;
        }

        private static Input CreateKeyboardInput(ushort key, bool keyUp)
        {
            return new Input
            {
                Type = InputKeyboard,
                Data = new InputUnion
                {
                    Keyboard = new KeyboardInput
                    {
                        VirtualKey = key,
                        Flags = keyUp ? KeyEventKeyUp : 0
                    }
                }
            };
        }
    }
}
