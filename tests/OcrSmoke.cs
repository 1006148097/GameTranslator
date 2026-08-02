using System;
using System.Drawing;
using System.Threading;
using GameTranslator;

internal static class OcrSmoke
{
    private static int Main(string[] args)
    {
        if (args.Length < 1 || args.Length > 2)
        {
            Console.Error.WriteLine(
                "Usage: OcrSmoke <image-path> [--ocr-only]");
            return 2;
        }

        using (var bitmap = new Bitmap(args[0]))
        {
            var engine = new WindowsOcrEngine();
            var result = engine
                .RecognizeAsync(bitmap, CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Console.OutputEncoding = new System.Text.UTF8Encoding(false);
            Console.WriteLine(result.Text);
            Console.WriteLine("OCR_SECONDS=" + result.Duration.TotalSeconds.ToString("0.000"));
            if (args.Length == 2 && args[1] == "--ocr-only")
            {
                return 0;
            }

            try
            {
                var provider = TranslationProviderFactory.Create("MyMemory Free");
                var translated = provider.TranslateAsync(
                    new TranslationRequest
                    {
                        Text = result.Text,
                        SourceLanguage = "auto",
                        TargetLanguage = "zh-Hans"
                    },
                    AppSettings.CreateDefault(),
                    CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                Console.WriteLine(translated.Text);
                Console.WriteLine(
                    "TRANSLATE_SECONDS="
                    + translated.Duration.TotalSeconds.ToString("0.000"));
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("TRANSLATE_ERROR_TYPE=" + error.GetType().FullName);
                Console.Error.WriteLine("TRANSLATE_ERROR_MESSAGE=" + error.Message);
                if (error.InnerException != null)
                {
                    Console.Error.WriteLine(
                        "TRANSLATE_INNER_TYPE="
                        + error.InnerException.GetType().FullName);
                    Console.Error.WriteLine(
                        "TRANSLATE_INNER_MESSAGE="
                        + error.InnerException.Message);
                }
                return 1;
            }
        }
        return 0;
    }
}
