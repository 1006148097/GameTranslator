using System;
using System.Text;
using System.Threading;
using GameTranslator;

internal static class TranslationSmoke
{
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine(
                "Usage: TranslationSmoke [--target <code>] <text>");
            return 2;
        }

        Console.OutputEncoding = new UTF8Encoding(false);
        try
        {
            var targetLanguage = "zh-Hans";
            var textStart = 0;
            if (args.Length >= 3
                && args[0].Equals(
                    "--target",
                    StringComparison.OrdinalIgnoreCase))
            {
                targetLanguage = args[1];
                textStart = 2;
            }
            var text = string.Join(
                " ",
                args,
                textStart,
                args.Length - textStart);
            var result = TranslationProviderFactory
                .Create("智能在线翻译")
                .TranslateAsync(
                    new TranslationRequest
                    {
                        Text = text,
                        SourceLanguage = "auto",
                        TargetLanguage = targetLanguage
                    },
                    AppSettings.CreateDefault(),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Console.WriteLine("PROVIDER=" + result.Provider);
            Console.WriteLine("TEXT=" + result.Text);
            Console.WriteLine(
                "TRANSLATE_SECONDS="
                + result.Duration.TotalSeconds.ToString("0.000"));
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.ToString());
            return 1;
        }
    }
}
