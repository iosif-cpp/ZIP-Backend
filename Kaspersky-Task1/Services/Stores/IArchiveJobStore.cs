using Kaspersky_Task1.Domain;

namespace Kaspersky_Task1.Services.Stores;

public interface IArchiveJobStore
{
    void Create(Guid id, IReadOnlyList<string> requestedFiles, string cacheKey);
    bool TryGet(Guid id, out ArchiveJobState state);
    bool TryUpdateStatus(Guid id, ArchiveJobStatus newStatus, string? error = null, byte[]? zipBytes = null);
}