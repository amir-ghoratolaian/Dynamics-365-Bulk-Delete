using System;
using System.Collections.Generic;

namespace BulkDeleteParallel.Models;

public class BatchResult
{
    public int Deleted { get; set; }

    public int Failed { get; set; }

    // Records where the fault was ObjectDoesNotExist — almost always a
    // retry re-submitting a batch that already succeeded server-side.
    // Not a real failure for a delete operation.
    public int AlreadyGone { get; set; }

    public double Seconds { get; set; }

    public IReadOnlyList<Guid> FailedIds { get; set; } = Array.Empty<Guid>();
}