using BulkDeleteParallel.Configuration;
using BulkDeleteParallel.Models;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace BulkDeleteParallel.Services;

public class BulkDeleteService(DeleteConfiguration Config, DataverseClientFactory Factory)
{
    private readonly DeleteStatistics _statistics = new();
    private readonly ConcurrentBag<Guid> _failedIds = new();

    public async Task RunAsync(CancellationToken token)
    {
        Console.WriteLine($"Started : {DateTime.Now}");
        Console.WriteLine();

        // Bounded channel finally gives QueueSize a purpose: paging and
        // deleting now run concurrently instead of read-then-delete.
        var channel = Channel.CreateBounded<Guid[]>(new BoundedChannelOptions(Config.QueueSize)
        {
            SingleWriter = true,
            SingleReader = false
        });

        var producerTask = ProduceAsync(channel.Writer, token);
        var consumerTasks = Enumerable.Range(0, Config.WorkerCount).Select(workerId => ConsumeAsync(workerId, channel.Reader, token)).ToArray();

        await Task.WhenAll(new[] { producerTask }.Concat(consumerTasks));

        if (!_failedIds.IsEmpty)
        {
            var path = $"failed-ids-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
            await File.WriteAllLinesAsync(path, _failedIds.Select(id => id.ToString()));
            Console.WriteLine();
            Console.WriteLine($"Failed IDs written to {path}");
        }

        Console.WriteLine();
        Console.WriteLine("==================================");
        Console.WriteLine("Bulk delete completed");
        Console.WriteLine($"Deleted     : {_statistics.Deleted:N0}");
        Console.WriteLine($"Already gone: {_statistics.AlreadyGone:N0} (retried batches, not real failures)");
        Console.WriteLine($"Failed      : {_statistics.Failed:N0}");
        Console.WriteLine($"Finished    : {DateTime.Now}");
    }

    private async Task ProduceAsync(ChannelWriter<Guid[]> writer, CancellationToken token)
    {
        Exception? failure = null;

        using var readerClient = Factory.Create();
        var reader = new RecordReader(readerClient, Config);

        try
        {
            await foreach (var batch in reader.ReadAsync(token))
            {
                await writer.WriteAsync(batch, token);
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            writer.Complete(failure);
        }
    }

    private async Task ConsumeAsync(int workerId, ChannelReader<Guid[]> reader, CancellationToken token)
    {
        Console.WriteLine($"Worker {workerId} started.");

        using var client = Factory.Create();
        var worker = new DeleteWorker(client, Config);

        await foreach (var batch in reader.ReadAllAsync(token))
        {
            var result = await worker.DeleteAsync(batch, token);

            _statistics.AddDeleted(result.Deleted);
            _statistics.AddFailed(result.Failed);
            _statistics.AddAlreadyGone(result.AlreadyGone);

            foreach (var id in result.FailedIds)
            {
                _failedIds.Add(id);
            }

            Console.WriteLine(
                $"Worker {workerId} | " +
                $"Deleted={result.Deleted} " +
                $"Failed={result.Failed} " +
                $"AlreadyGone={result.AlreadyGone} " +
                $"Time={result.Seconds:F1}s " +
                $"Total Deleted={_statistics.Deleted:N0}");
        }

        Console.WriteLine($"Worker {workerId} finished.");
    }
}