namespace BulkDeleteParallel.Configuration;

public class DeleteConfiguration
{
    public string ConnectionString { get; set; } = string.Empty;

    public string EntityLogicalName { get; set; } = string.Empty;

    public string FilterXml { get; set; } = string.Empty;

    public int FetchPageSize { get; set; } = 5000;

    public int BatchSize { get; set; } = 500;

    public int WorkerCount { get; set; } = 8;

    public int QueueSize { get; set; } = 20;

    public int RequestTimeoutMinutes { get; set; } = 30;

    public int RetryCount { get; set; } = 5;

    public bool BypassSyncPlugins { get; set; }
}