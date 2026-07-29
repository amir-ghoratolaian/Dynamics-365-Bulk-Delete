using System.Threading;

namespace BulkDeleteParallel.Models;

public class DeleteStatistics
{
    private long _deleted;
    private long _failed;
    private long _alreadyGone;
    private long _processed;

    public long Deleted => _deleted;

    public long Failed => _failed;

    public long AlreadyGone => _alreadyGone;

    public long Processed => _processed;

    public void AddDeleted(int count)
    {
        Interlocked.Add(ref _deleted, count);
        Interlocked.Add(ref _processed, count);
    }

    public void AddFailed(int count)
    {
        Interlocked.Add(ref _failed, count);
        Interlocked.Add(ref _processed, count);
    }

    public void AddAlreadyGone(int count)
    {
        Interlocked.Add(ref _alreadyGone, count);
        Interlocked.Add(ref _processed, count);
    }
}