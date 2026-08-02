using System;
using System.Threading;
using System.Threading.Tasks;

namespace GameTranslator
{
    internal sealed class TranslationTimeoutException
        : OperationCanceledException
    {
        public TranslationTimeoutException()
            : base("翻译超时（10 秒），本次操作已取消。")
        {
        }
    }

    internal static class TranslationOperation
    {
        internal const int TimeoutSeconds = 10;

        internal static async Task<TranslationResult> RunAsync(
            ITranslationProvider provider,
            TranslationRequest request,
            AppSettings settings,
            CancellationToken cancellationToken)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }

            using (var timeoutCancellation =
                new CancellationTokenSource())
            using (var linkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutCancellation.Token))
            {
                timeoutCancellation.CancelAfter(
                    TimeoutSeconds * 1000);
                var providerTask = provider.TranslateAsync(
                    request,
                    settings,
                    linkedCancellation.Token);
                var cancellationTask = Task.Delay(
                    Timeout.Infinite,
                    linkedCancellation.Token);
                try
                {
                    var completed = await Task.WhenAny(
                        providerTask,
                        cancellationTask);
                    if (completed != providerTask)
                    {
                        ObserveFailure(providerTask);
                        cancellationToken.ThrowIfCancellationRequested();
                        throw new TranslationTimeoutException();
                    }

                    var result = await providerTask;
                    cancellationToken.ThrowIfCancellationRequested();
                    if (timeoutCancellation.IsCancellationRequested)
                    {
                        throw new TranslationTimeoutException();
                    }
                    timeoutCancellation.CancelAfter(Timeout.Infinite);
                    return result;
                }
                catch (TranslationTimeoutException)
                {
                    linkedCancellation.Cancel();
                    throw;
                }
                catch (OperationCanceledException)
                {
                    if (timeoutCancellation.IsCancellationRequested
                        && !cancellationToken.IsCancellationRequested)
                    {
                        throw new TranslationTimeoutException();
                    }
                    throw;
                }
            }
        }

        private static void ObserveFailure(Task<TranslationResult> task)
        {
            task.ContinueWith(
                completed =>
                {
                    var ignored = completed.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted
                | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
