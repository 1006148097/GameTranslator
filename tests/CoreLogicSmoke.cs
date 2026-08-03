using System;
using System.Collections;
using System.Reflection;
using GameTranslator;

internal static class CoreLogicSmoke
{
    private static int Main()
    {
        if (AppSettings.CreateDefault()
            .TextReplacementTargetLanguage != "en")
        {
            Console.Error.WriteLine(
                "DEFAULT_TEXT_REPLACEMENT_LANGUAGE_FAILED");
            return 1;
        }

        var smartProvider = TranslationProviderFactory.Create(
            "智能在线翻译");
        var primaryProviderField = typeof(ResilientTranslationProvider)
            .GetField("_primary", BindingFlags.Instance | BindingFlags.NonPublic);
        var primaryProvider = primaryProviderField == null
            ? null
            : primaryProviderField.GetValue(smartProvider);
        if (!(primaryProvider is GoogleTranslationProvider))
        {
            Console.Error.WriteLine("SMART_PROVIDER_GOOGLE_PRIORITY_FAILED");
            return 1;
        }

        string localTranslation;
        if (!GameTranslationGlossary.TryTranslateText(
                "Continue\nExit Game",
                "zh-Hans",
                out localTranslation)
            || localTranslation != "继续\n退出游戏")
        {
            Console.Error.WriteLine(
                "LOCAL_GAME_GLOSSARY_FAILED=" + localTranslation);
            return 1;
        }

        var enhancedTerms = GameTranslationGlossary.EnhanceResult(
            "DPS and HP",
            "085 和 hp",
            "zh-Hans");
        if (enhancedTerms != "DPS 和 HP")
        {
            Console.Error.WriteLine(
                "GAME_TERM_PROTECTION_FAILED=" + enhancedTerms);
            return 1;
        }

        var globalMicrosoftUrl =
            MicrosoftTranslationProvider.BuildTranslateUrl(
                "https://api.cognitive.microsofttranslator.com/");
        var customMicrosoftUrl =
            MicrosoftTranslationProvider.BuildTranslateUrl(
                "https://sample.cognitiveservices.azure.com");
        var chinaMicrosoftUrl =
            MicrosoftTranslationProvider.BuildTranslateUrl(
                "https://api.translator.azure.cn/");
        if (globalMicrosoftUrl
                != "https://api.cognitive.microsofttranslator.com/translate"
            || customMicrosoftUrl
                != "https://sample.cognitiveservices.azure.com/translator/text/v3.0/translate"
            || chinaMicrosoftUrl
                != "https://api.translator.azure.cn/translate")
        {
            Console.Error.WriteLine(
                "MICROSOFT_ENDPOINT_MAPPING_FAILED="
                + globalMicrosoftUrl + "|" + customMicrosoftUrl
                + "|" + chinaMicrosoftUrl);
            return 1;
        }

        if (TranslationLanguages.ToGoogleCode("zh-Hans") != "zh-CN"
            || TranslationLanguages.ToGoogleCode("zh-Hant") != "zh-TW"
            || TranslationLanguages.ToDeepLTargetCode("pt-BR") != "PT-BR"
            || TranslationLanguages.ToMicrosoftCode("zh-Hant") != "zh-Hant"
            || TranslationLanguages.ToLibreCode("pt-BR") != "pb"
            || TranslationLanguages.ToMyMemoryCode("zh-Hant") != "zh-TW")
        {
            Console.Error.WriteLine("LANGUAGE_MAPPING_FAILED");
            Console.Error.WriteLine(
                "GOOGLE_HANS="
                + TranslationLanguages.ToGoogleCode("zh-Hans"));
            Console.Error.WriteLine(
                "GOOGLE_HANT="
                + TranslationLanguages.ToGoogleCode("zh-Hant"));
            Console.Error.WriteLine(
                "DEEPL_PT="
                + TranslationLanguages.ToDeepLTargetCode("pt-BR"));
            Console.Error.WriteLine(
                "MS_HANT="
                + TranslationLanguages.ToMicrosoftCode("zh-Hant"));
            Console.Error.WriteLine(
                "LIBRE_PT="
                + TranslationLanguages.ToLibreCode("pt-BR"));
            Console.Error.WriteLine(
                "MEMORY_HANT="
                + TranslationLanguages.ToMyMemoryCode("zh-Hant"));
            return 1;
        }

        var translationText = typeof(TranslationProviderFactory).Assembly
            .GetType("GameTranslator.TranslationText", true);
        var splitMethod = translationText.GetMethod(
            "SplitContextChunks",
            BindingFlags.Static | BindingFlags.Public);
        var chunks = splitMethod.Invoke(
            null,
            new object[] { "😀😀", 4 }) as IEnumerable;
        var count = 0;
        foreach (var chunk in chunks)
        {
            if (!string.Equals(
                Convert.ToString(chunk),
                "😀",
                StringComparison.Ordinal))
            {
                Console.Error.WriteLine("UNICODE_CHUNK_FAILED");
                return 1;
            }
            count++;
        }
        if (count != 2)
        {
            Console.Error.WriteLine("UNICODE_CHUNK_COUNT_FAILED");
            return 1;
        }

        var scoreMethod = typeof(WindowsOcrEngine).GetMethod(
            "ScoreKoreanCandidate",
            BindingFlags.Static | BindingFlags.NonPublic);
        var correctKorean =
            "게임중에는 한글을 사용하는게 오히려 안좋을수도 있어";
        var noisyKorean =
            "01 즈     는 려          그        그\n" + correctKorean;
        var fragmentedKorean =
            "게임중어\n느 ㅁ 2 크\n사용하는게 오히려\n아조으스\nㄴㄴ = =\n도 있";
        var correctScore = Convert.ToInt32(
            scoreMethod.Invoke(null, new object[] { correctKorean }));
        var noisyScore = Convert.ToInt32(
            scoreMethod.Invoke(null, new object[] { noisyKorean }));
        var fragmentedScore = Convert.ToInt32(
            scoreMethod.Invoke(null, new object[] { fragmentedKorean }));
        if (correctScore <= noisyScore || correctScore <= fragmentedScore)
        {
            Console.Error.WriteLine("KOREAN_CANDIDATE_SCORING_FAILED");
            return 1;
        }
        var mixedLatinScore = Convert.ToInt32(scoreMethod.Invoke(
            null,
            new object[] { "나는 아직 탱크와 DPS를 정하지 않았어" }));
        var digitSubstitutionScore = Convert.ToInt32(scoreMethod.Invoke(
            null,
            new object[] { "나는 아직 탱크와 085를 정하지 않았어" }));
        if (mixedLatinScore <= digitSubstitutionScore)
        {
            Console.Error.WriteLine("KOREAN_ACRONYM_SCORING_FAILED");
            return 1;
        }

        var normalizeMethod = typeof(WindowsOcrEngine).GetMethod(
            "NormalizeOcrLines",
            BindingFlags.Static | BindingFlags.NonPublic);
        var multiBubbleRaw =
            "날 보고 있었다는건가\n"
            + "오늘은 내 친구가 없어서 번역을 못해\n"
            + "번역 어플을 써봤는데 이상한거 같아\n"
            + "ㅅㅅ\n"
            + "그냥 보여요 라고 번역이 됐어";
        var multiBubbleExpected =
            "날 보고 있었다는건가\n"
            + "오늘은 내 친구가 없어서 번역을 못해\n"
            + "번역 어플을 써봤는데 이상한거 같아\n"
            + "그냥 보여요 라고 번역이 됐어";
        var multiBubbleNormalized = Convert.ToString(
            normalizeMethod.Invoke(null, new object[] { multiBubbleRaw }));
        if (!string.Equals(
            multiBubbleNormalized,
            multiBubbleExpected,
            StringComparison.Ordinal))
        {
            Console.Error.WriteLine("KOREAN_JAMO_NOISE_FILTER_FAILED");
            return 1;
        }

        var parseWindowsCandidates = typeof(WindowsOcrEngine).GetMethod(
            "ParseWindowsOcrCandidates",
            BindingFlags.Static | BindingFlags.NonPublic);
        var selectBestOcr = typeof(WindowsOcrEngine).GetMethod(
            "SelectBestOcrText",
            BindingFlags.Static | BindingFlags.NonPublic);
        var japaneseCandidates = parseWindowsCandidates.Invoke(
            null,
            new object[]
            {
                "[{\"Text\":\"セ ー ブ デ ー タ？\",\"Score\":220,\"Language\":\"ja\"},"
                + "{\"Text\":\"1-27주룹\",\"Score\":60,\"Language\":\"ko\"}]"
            });
        var selectedJapanese = Convert.ToString(selectBestOcr.Invoke(
            null,
            new[] { (object)"1-27-2주룹2스즈초971?", japaneseCandidates }));
        if (!string.Equals(
            selectedJapanese,
            "セーブデータ？",
            StringComparison.Ordinal))
        {
            Console.Error.WriteLine(
                "MULTILINGUAL_OCR_SELECTION_FAILED=" + selectedJapanese);
            return 1;
        }

        var koreanCandidates = parseWindowsCandidates.Invoke(
            null,
            new object[]
            {
                "[{\"Text\":\"게임을 시 작 합 니 다\",\"Score\":180,\"Language\":\"ko\"},"
                + "{\"Text\":\"剛号人丨\",\"Score\":40,\"Language\":\"zh\"}]"
            });
        var selectedKorean = Convert.ToString(selectBestOcr.Invoke(
            null,
            new[] { (object)"게임을 시작합니다", koreanCandidates }));
        if (!string.Equals(
            selectedKorean,
            "게임을 시작합니다",
            StringComparison.Ordinal))
        {
            Console.Error.WriteLine(
                "KOREAN_OCR_SELECTION_FAILED=" + selectedKorean);
            return 1;
        }

        var acronymCandidates = parseWindowsCandidates.Invoke(
            null,
            new object[]
            {
                "[{\"Text\":\"나는 아직 탱크와 DPS를 등급을 정하지 않았어\","
                + "\"Score\":445,\"Language\":\"ko\"}]"
            });
        var selectedAcronym = Convert.ToString(selectBestOcr.Invoke(
            null,
            new[]
            {
                (object)"나는 아직 탱크와 085를 등급을 정하지 않았어",
                acronymCandidates
            }));
        if (!string.Equals(
            selectedAcronym,
            "나는 아직 탱크와 DPS를 등급을 정하지 않았어",
            StringComparison.Ordinal))
        {
            Console.Error.WriteLine(
                "KOREAN_ACRONYM_SELECTION_FAILED=" + selectedAcronym);
            return 1;
        }

        var coloredKoreanCandidates = parseWindowsCandidates.Invoke(
            null,
            new object[]
            {
                "[{\"Text\":\"위험! 즉시 대피 하세요\",\"Score\":235,\"Language\":\"ko\"},"
                + "{\"Text\":\"早尾人古\",\"Score\":30,\"Language\":\"zh\"}]"
            });
        var selectedColoredKorean = Convert.ToString(selectBestOcr.Invoke(
            null,
            new[] { (object)"[리 1 제 즉 니브 마 몰", coloredKoreanCandidates }));
        if (!string.Equals(
            selectedColoredKorean,
            "위험! 즉시 대피 하세요",
            StringComparison.Ordinal))
        {
            Console.Error.WriteLine(
                "KOREAN_QUALITY_SELECTION_FAILED=" + selectedColoredKorean);
            return 1;
        }

        var singleCandidate = parseWindowsCandidates.Invoke(
            null,
            new object[]
            {
                "{\"Text\":\"Press Start\",\"Score\":80,\"Language\":\"en\"}"
            }) as IEnumerable;
        var singleCandidateCount = 0;
        foreach (var ignored in singleCandidate)
        {
            singleCandidateCount++;
        }
        if (singleCandidateCount != 1)
        {
            Console.Error.WriteLine("SINGLE_OCR_LANGUAGE_PARSE_FAILED");
            return 1;
        }

        Console.WriteLine("LANGUAGE_MAPPING_OK");
        Console.WriteLine("UNICODE_CHUNK_OK");
        Console.WriteLine("KOREAN_CANDIDATE_SCORING_OK");
        Console.WriteLine("KOREAN_ACRONYM_SCORING_OK");
        Console.WriteLine("KOREAN_JAMO_NOISE_FILTER_OK");
        Console.WriteLine("MULTILINGUAL_OCR_SELECTION_OK");
        Console.WriteLine("KOREAN_QUALITY_SELECTION_OK");
        Console.WriteLine("KOREAN_ACRONYM_SELECTION_OK");
        Console.WriteLine("SINGLE_OCR_LANGUAGE_PARSE_OK");
        return 0;
    }
}
