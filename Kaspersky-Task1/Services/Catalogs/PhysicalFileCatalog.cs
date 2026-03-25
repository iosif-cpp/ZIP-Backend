using Kaspersky_Task1.Configuration;

namespace Kaspersky_Task1.Services.Catalogs;

public sealed class PhysicalFileCatalog : IFileCatalog
{
    private readonly ArchiveOptions _opts;

    public PhysicalFileCatalog(ArchiveOptions opts) => _opts = opts;

    public Task<IReadOnlyList<string>> ListFileNamesAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!Directory.Exists(_opts.FilesDir))
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        var names = new DirectoryInfo(_opts.FilesDir)
            .GetFiles()
            .Select(f => f.Name)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(names);
    }

    public Task<IReadOnlyList<string>> ResolveRequestedFilesAsync(IEnumerable<string> requestedFileNames, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!Directory.Exists(_opts.FilesDir))
            throw new ArgumentException("Files directory does not exist.");

        var resolved = new List<string>();

        foreach (var name in requestedFileNames)
        {
            ValidateFileName(name);

            var fullPath = Path.Combine(_opts.FilesDir, name);
            if (!File.Exists(fullPath))
                throw new ArgumentException($"File '{name}' does not exist.");

            resolved.Add(fullPath);
        }

        return Task.FromResult<IReadOnlyList<string>>(resolved);
    }

    private static void ValidateFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.");

        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
            throw new ArgumentException($"Invalid file name '{fileName}'.");

        if (!Path.GetFileName(fileName).Equals(fileName, StringComparison.Ordinal))
            throw new ArgumentException($"Invalid file name '{fileName}'.");

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException($"Invalid file name '{fileName}'.");
    }
}

