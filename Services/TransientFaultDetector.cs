using System;

namespace BulkDeleteParallel.Services;

public static class TransientFaultDetector
{
    public static bool IsTransient(Exception ex)
    {
        var message =
            ex.ToString()
                .ToLowerInvariant();

        return
            message.Contains("timeout") ||
            message.Contains("timed out") ||
            message.Contains("429") ||
            message.Contains("thrott") ||
            message.Contains("server busy") ||
            message.Contains("temporarily unavailable") ||
            message.Contains("gateway");
    }
}