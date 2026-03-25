namespace Kaspersky_Task1.Domain;

public sealed class ArchiveJobState
{
    public Guid ProcessId { get; init; }
    public string CacheKey { get; init; } = "";
    public List<string> RequestedFiles { get; init; } = new();
    public ArchiveJobStatus Status { get; set; } = ArchiveJobStatus.Pending;
    public string? Error { get; set; }
    public byte[]? ZipBytes { get; set; }
}