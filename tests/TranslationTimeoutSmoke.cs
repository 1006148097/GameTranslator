using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using GameTranslator;

internal static class TranslationTimeoutSmoke
{
    private sealed class SlowProvider : ITranslationProvider
    {
        public string Name { get { return "Slow test provider"; } }

        public async Task<TranslationResult> TranslateAsync(
            TranslationRequest request,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            // Deliberately ignore cancellation. The operation wrapper must
            // still return at its own deadline.
            await Task.Delay(30000);
            return new TranslationResult { Text = "too late" };
        }
    }

    private static int Main()
    {
        var watch = Stopwatch.StartNew();
        var timeoutPassed = false;
        try
        {
            TranslationOperation.RunAsync(
                new SlowProvider(),
                new TranslationRequest
                {
                    Text = "timeout",
                    SourceLanguage = "auto",
                    TargetLanguage = "zh-Hans"
                },
                AppSettings.CreateDefault(),
                CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Console.Error.WriteLine("Expected timeout, but translation completed.");
            return 1;
        }
        catch (TranslationTimeoutException error)
        {
            watch.Stop();
            Console.WriteLine(error.Message);
            Console.WriteLine(
                "ELAPSED_SECONDS="
                + watch.Elapsed.TotalSeconds.ToString("0.00"));
            timeoutPassed = watch.Elapsed.TotalSeconds >= 9.5
                && watch.Elapsed.TotalSeconds <= 12.5;
        }

        var manualWatch = Stopwatch.StartNew();
        using (var manualCancellation =
            new CancellationTokenSource())
        {
            manualCancellation.CancelAfter(150);
            try
            {
                TranslationOperation.RunAsync(
                    new SlowProvider(),
                    new TranslationRequest
                    {
                        Text = "manual cancellation",
                        SourceLanguage = "auto",
                        TargetLanguage = "zh-Hans"
                    },
                    AppSettings.CreateDefault(),
                    manualCancellation.Token)
                    .GetAwaiter()
                    .GetResult();
                Console.Error.WriteLine(
                    "Expected manual cancellation, but translation completed.");
                return 1;
            }
            catch (TranslationTimeoutException)
            {
                Console.Error.WriteLine(
                    "Manual cancellation was incorrectly reported as timeout.");
                return 1;
            }
            catch (OperationCanceledException)
            {
                manualWatch.Stop();
                Console.WriteLine(
                    "MANUAL_CANCEL_SECONDS="
                    + manualWatch.Elapsed.TotalSeconds.ToString("0.00"));
            }
        }
        return timeoutPassed && manualWatch.Elapsed.TotalSeconds < 2
            ? 0
            : 1;
    }
}
