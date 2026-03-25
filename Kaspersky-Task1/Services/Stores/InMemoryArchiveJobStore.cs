using Kaspersky_Task1.Domain;

namespace Kaspersky_Task1.Services.Stores;

using System.Collections.Concurrent;

public sealed class InMemoryArchiveJobStore : IArchiveJobStore
{
    private readonly ConcurrentDictionary<Guid, ArchiveJobState> _jobs = new();

    public void Create(Guid id, IReadOnlyList<string> requestedFiles, string cacheKey)
    {
        var job = new ArchiveJobState
        {
            ProcessId = id,
            CacheKey = cacheKey,
            RequestedFiles = requestedFiles.ToList()
        };
        _jobs[id] = job;
    }

    public bool TryGet(Guid id, out ArchiveJobState state) => _jobs.TryGetValue(id, out state!);

    public bool TryUpdateStatus(Guid id, ArchiveJobStatus newStatus, string? error = null, byte[]? zipBytes = null)
    {
        if (!_jobs.TryGetValue(id, out var job)) return false;

        lock (job)
        {
            job.Status = newStatus;
            if (error is not null) job.Error = error;
            if (zipBytes is not null) job.ZipBytes = zipBytes;
            return true;
        }
    }
}