using System;
using System.Threading.Tasks;

namespace BulkDeleteParallel.Services;

public static class RetryHelper
{
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> action, int maxRetries)
    {
        Exception? lastException = null;

        for (int retry = 0; retry <= maxRetries; retry++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
                when (TransientFaultDetector.IsTransient(ex))
            {
                lastException = ex;

                if (retry == maxRetries)
                    break;

                var delay =
                    TimeSpan.FromSeconds(Math.Pow(2, retry + 1));

                Console.WriteLine(
                    $"Retry {retry + 1}/{maxRetries}. " +
                    $"Waiting {delay.TotalSeconds}s");

                await Task.Delay(delay);
            }
        }

        throw lastException!;
    }
}