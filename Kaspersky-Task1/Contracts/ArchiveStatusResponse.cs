namespace Kaspersky_Task1.Contracts;

public sealed record ArchiveStatusResponse
(
    Guid ProcessId,
    string Status,
    string? Error
);