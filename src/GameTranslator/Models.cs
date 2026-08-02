using System;
using System.Drawing;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace GameTranslator
{
    [DataContract]
    public sealed class AppSettings
    {
        [DataMember] public string Hotkey { get; set; }
        [DataMember] public string ToggleOverlayHotkey { get; set; }
        [DataMember] public string TextReplacementHotkey { get; set; }
        [DataMember] public string TextReplacementTargetLanguage { get; set; }
        [DataMember] public double FontSize { get; set; }
        [DataMember] public double BackgroundOpacity { get; set; }
        [DataMember] public double OverlayLeft { get; set; }
        [DataMember] public double OverlayTop { get; set; }
        [DataMember] public string TranslationProvider { get; set; }
        [DataMember] public string DeepLApiKey { get; set; }
        [DataMember] public bool DeepLUseFreeApi { get; set; }
        [DataMember] public string MicrosoftTranslatorKey { get; set; }
        [DataMember] public string MicrosoftTranslatorRegion { get; set; }
        [DataMember] public string LibreTranslateEndpoint { get; set; }
        [DataMember] public string LibreTranslateApiKey { get; set; }
        [DataMember] public string LingvaEndpoint { get; set; }

        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                Hotkey = "F8",
                ToggleOverlayHotkey = "F7",
                TextReplacementHotkey = "F6",
                TextReplacementTargetLanguage = "zh-Hans",
                FontSize = 22,
                BackgroundOpacity = 0.22,
                OverlayLeft = 80,
                OverlayTop = 80,
                TranslationProvider = "智能在线翻译",
                DeepLUseFreeApi = true,
                LibreTranslateEndpoint =
                    "https://translate.argosopentech.com",
                LingvaEndpoint = "https://lingva.lunar.icu"
            };
        }
    }

    public sealed class OcrResult
    {
        public string Text { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public sealed class TranslationRequest
    {
        public string Text { get; set; }
        public string SourceLanguage { get; set; }
        public string TargetLanguage { get; set; }
    }

    public sealed class TranslationResult
    {
        public string Text { get; set; }
        public TimeSpan Duration { get; set; }
        public string Provider { get; set; }
    }

    public interface IOcrEngine
    {
        Task<OcrResult> RecognizeAsync(Bitmap bitmap, CancellationToken cancellationToken);
    }

    public interface ITranslationProvider
    {
        string Name { get; }
        Task<TranslationResult> TranslateAsync(
            TranslationRequest request,
            AppSettings settings,
            CancellationToken cancellationToken);
    }

    internal static class HotkeyRules
    {
        internal static bool IsUnsafeTextReplacementHotkey(string hotkey)
        {
            var normalized = (hotkey ?? "")
                .Replace(" ", "")
                .ToUpperInvariant();
            return normalized == "CTRL+C"
                || normalized == "CTRL+V"
                || normalized == "DELETE";
        }
    }
}
