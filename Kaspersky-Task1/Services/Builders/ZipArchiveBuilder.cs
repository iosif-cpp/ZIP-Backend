using System.IO.Compression;
using Kaspersky_Task1.Configuration;

namespace Kaspersky_Task1.Services.Builders;

public sealed class ZipArchiveBuilder : IArchiveBuilder
{
    public async Task<byte[]> BuildZipAsync(IEnumerable<string> absoluteFilePaths, CancellationToken ct)
    {
        await using var ms = new MemoryStream();
        using var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true);

        foreach (var file in absoluteFilePaths)
        {
            ct.ThrowIfCancellationRequested();

            var entryName = Path.GetFileName(file);
            var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);

            await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var entryStream = entry.Open();
            await input.CopyToAsync(entryStream, ct);
        }

        archive.Dispose();
        return ms.ToArray();
    }
}

