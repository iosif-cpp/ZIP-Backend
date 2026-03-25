namespace Kaspersky_Task1.Services.Catalogs;

public interface IFileCatalog
{
    Task<IReadOnlyList<string>> ListFileNamesAsync(CancellationToken ct);
    Task<IReadOnlyList<string>> ResolveRequestedFilesAsync(IEnumerable<string> requestedFileNames, CancellationToken ct);
}

