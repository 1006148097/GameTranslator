using System;
using System.Collections.Generic;

namespace GameTranslator
{
    public sealed class TranslationLanguageOption
    {
        public string Code { get; private set; }
        public string DisplayName { get; private set; }

        public TranslationLanguageOption(string code, string displayName)
        {
            Code = code;
            DisplayName = displayName;
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public static class TranslationLanguages
    {
        private static readonly TranslationLanguageOption[] Options =
        {
            new TranslationLanguageOption("zh-Hans", "简体中文"),
            new TranslationLanguageOption("zh-Hant", "繁体中文"),
            new TranslationLanguageOption("en", "英语"),
            new TranslationLanguageOption("ja", "日语"),
            new TranslationLanguageOption("ko", "韩语"),
            new TranslationLanguageOption("fr", "法语"),
            new TranslationLanguageOption("de", "德语"),
            new TranslationLanguageOption("es", "西班牙语"),
            new TranslationLanguageOption("pt-BR", "葡萄牙语（巴西）"),
            new TranslationLanguageOption("ru", "俄语")
        };

        public static IList<TranslationLanguageOption> GetOptions()
        {
            return new List<TranslationLanguageOption>(Options);
        }

        public static bool IsSupported(string code)
        {
            foreach (var option in Options)
            {
                if (option.Code.Equals(
                    code,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static string Normalize(string code)
        {
            foreach (var option in Options)
            {
                if (option.Code.Equals(
                    code,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return option.Code;
                }
            }
            return "zh-Hans";
        }

        public static string GetDisplayName(string code)
        {
            var normalized = Normalize(code);
            foreach (var option in Options)
            {
                if (option.Code == normalized)
                {
                    return option.DisplayName;
                }
            }
            return "简体中文";
        }

        public static string ToGoogleCode(string code)
        {
            switch (Normalize(code))
            {
                case "zh-Hans": return "zh-CN";
                case "zh-Hant": return "zh-TW";
                case "pt-BR": return "pb";
                default: return Normalize(code);
            }
        }

        public static string ToDeepLTargetCode(string code)
        {
            switch (Normalize(code))
            {
                case "zh-Hans": return "ZH-HANS";
                case "zh-Hant": return "ZH-HANT";
                case "en": return "EN-US";
                case "pt-BR": return "PT-BR";
                default: return Normalize(code).ToUpperInvariant();
            }
        }

        public static string ToDeepLSourceCode(string code)
        {
            var normalized = Normalize(code);
            if (normalized == "zh-Hans" || normalized == "zh-Hant")
            {
                return "ZH";
            }
            if (normalized == "pt-BR")
            {
                return "PT";
            }
            return normalized.ToUpperInvariant();
        }

        public static string ToMicrosoftCode(string code)
        {
            return Normalize(code);
        }

        public static string ToLibreCode(string code)
        {
            switch (Normalize(code))
            {
                case "zh-Hans": return "zh";
                case "zh-Hant": return "zt";
                case "pt-BR": return "pb";
                default: return Normalize(code);
            }
        }

        public static string ToMyMemoryCode(string code)
        {
            switch (Normalize(code))
            {
                case "zh-Hans": return "zh-CN";
                case "zh-Hant": return "zh-TW";
                default: return Normalize(code);
            }
        }
    }
}
