namespace Kaspersky_Task1.Services.Builders;

public interface IArchiveBuilder
{
    Task<byte[]> BuildZipAsync(IEnumerable<string> absoluteFilePaths, CancellationToken ct);
}

