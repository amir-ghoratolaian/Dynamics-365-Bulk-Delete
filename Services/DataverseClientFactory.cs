using BulkDeleteParallel.Configuration;
using Microsoft.PowerPlatform.Dataverse.Client;
using System;

namespace BulkDeleteParallel.Services;

public class DataverseClientFactory
{
    private readonly DeleteConfiguration _config;
    private readonly object _lock = new();
    private ServiceClient? _masterClient;

    public DataverseClientFactory(DeleteConfiguration config)
    {
        _config = config;

        // Must be set before the first ServiceClient is constructed.
        // Default is 2 minutes, which large ExecuteMultiple batches can
        // legitimately exceed under load.
        ServiceClient.MaxConnectionTimeout =
            TimeSpan.FromMinutes(_config.RequestTimeoutMinutes);
    }

    // First call authenticates fully and becomes the master connection.
    // Every subsequent call clones it instead of paying for another
    // OAuth handshake — Clone() is the SDK's supported pattern for
    // handing out additional thread-safe connections.
    public ServiceClient Create()
    {
        lock (_lock)
        {
            if (_masterClient == null)
            {
                _masterClient = new ServiceClient(_config.ConnectionString);

                if (!_masterClient.IsReady)
                {
                    throw new Exception(
                        $"Dataverse connection failed: {_masterClient.LastError}");
                }

                return _masterClient;
            }
        }

        var clone = _masterClient.Clone();

        if (!clone.IsReady)
        {
            throw new Exception($"Dataverse clone connection failed: {clone.LastError}");
        }

        return clone;
    }
}