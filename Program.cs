using BulkDeleteParallel.Configuration;
using BulkDeleteParallel.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Threading;

var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", false).Build();

var config = configuration.GetSection("DeleteConfiguration").Get<DeleteConfiguration>();

if (config == null)
{
    throw new Exception("DeleteConfiguration missing");
}

using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("Cancellation requested, finishing in-flight batches...");
    cts.Cancel();
};

var factory = new DataverseClientFactory(config);
var service = new BulkDeleteService(config, factory);

try
{
    await service.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Cancelled.");
    Environment.ExitCode = 1;
}
catch (Exception ex)
{
    Console.WriteLine($"Fatal error: {ex}");
    Environment.ExitCode = 1;
}