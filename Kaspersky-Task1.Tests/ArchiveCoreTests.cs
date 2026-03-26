using System.IO.Compression;
using Kaspersky_Task1.Configuration;
using Kaspersky_Task1.Domain;
using Kaspersky_Task1.Services.Builders;
using Kaspersky_Task1.Services.Caches;
using Kaspersky_Task1.Services.Catalogs;
using Kaspersky_Task1.Services.Stores;
using Xunit;

namespace Kaspersky_Task1.Tests;

public sealed class ArchiveCoreTests
{
    [Fact]
    public async Task PhysicalFileCatalog_ResolveRequestedFilesAsync_ResolvesToAbsolutePaths()
    {
        var root = CreateTempDir();
        try
        {
            var filesDir = Path.Combine(root, "files");
            Directory.CreateDirectory(filesDir);
            var file1 = Path.Combine(filesDir, "a.txt");
            var file2 = Path.Combine(filesDir, "b.txt");
            await File.WriteAllTextAsync(file1, "A");
            await File.WriteAllTextAsync(file2, "B");

            var opts = new ArchiveOptions { FilesDir = filesDir };
            var catalog = new PhysicalFileCatalog(opts);

            var result = await catalog.ResolveRequestedFilesAsync(new[] { "a.txt", "b.txt" }, CancellationToken.None);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, p => p.EndsWith(Path.DirectorySeparatorChar + "a.txt", StringComparison.Ordinal));
            Assert.Contains(result, p => p.EndsWith(Path.DirectorySeparatorChar + "b.txt", StringComparison.Ordinal));
            Assert.All(result, p => Assert.True(Path.IsPathRooted(p)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PhysicalFileCatalog_ResolveRequestedFilesAsync_ThrowsOnInvalidName()
    {
        var root = CreateTempDir();
        try
        {
            var filesDir = Path.Combine(root, "files");
            Directory.CreateDirectory(filesDir);
            await File.WriteAllTextAsync(Path.Combine(filesDir, "a.txt"), "A");

            var opts = new ArchiveOptions { FilesDir = filesDir };
            var catalog = new PhysicalFileCatalog(opts);

            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await catalog.ResolveRequestedFilesAsync(new[] { "../a.txt" }, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await catalog.ResolveRequestedFilesAsync(new[] { "..\\a.txt" }, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await catalog.ResolveRequestedFilesAsync(new[] { "a/b.txt" }, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ArchiveCache_GetCacheKeyForFiles_IsOrderIndependentAndCaseInsensitive()
    {
        var root = CreateTempDir();
        try
        {
            var opts = new ArchiveOptions { FilesDir = Path.Combine(root, "files") };
            var cache = new ArchiveCache(opts);

            var p1 = cache.GetCacheKeyForFiles(new[] { "B.TXT", "a.txt" });
            var p2 = cache.GetCacheKeyForFiles(new[] { "a.txt", "b.txt" });
            var p3 = cache.GetCacheKeyForFiles(new[] { "a.txt", "a.txt", "B.TXT" });

            Assert.Equal(p1, p2);
            Assert.Equal(p1, p3);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ZipArchiveBuilder_BuildZipAsync_CreatesZipWithExpectedEntries()
    {
        var root = CreateTempDir();
        try
        {
            var filesDir = Path.Combine(root, "files");
            Directory.CreateDirectory(filesDir);
            var aPath = Path.Combine(filesDir, "a.txt");
            var bPath = Path.Combine(filesDir, "b.txt");
            await File.WriteAllTextAsync(aPath, "A");
            await File.WriteAllTextAsync(bPath, "B");

            var builder = new ZipArchiveBuilder();
            var zipBytes = await builder.BuildZipAsync(new[] { aPath, bPath }, CancellationToken.None);
            Assert.NotNull(zipBytes);
            Assert.True(zipBytes.Length > 0);

            using var ms = new MemoryStream(zipBytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);
            var names = zip.Entries.Select(e => e.FullName).ToList();
            Assert.Contains("a.txt", names);
            Assert.Contains("b.txt", names);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void InMemoryArchiveJobStore_TryUpdateStatus_UpdatesState()
    {
        var store = new InMemoryArchiveJobStore();
        var id = Guid.NewGuid();
        var cacheKey = Guid.NewGuid().ToString("N");
        store.Create(id, new[] { "a.txt", "b.txt" }, cacheKey);

        Assert.True(store.TryGet(id, out var job1));
        Assert.Equal(ArchiveJobStatus.Pending, job1.Status);
        Assert.Equal(cacheKey, job1.CacheKey);

        Assert.True(store.TryUpdateStatus(id, ArchiveJobStatus.Processing));
        Assert.True(store.TryGet(id, out var job2));
        Assert.Equal(ArchiveJobStatus.Processing, job2.Status);
        Assert.Null(job2.Error);

        var zipBytes = new byte[] { 1, 2, 3 };
        Assert.True(store.TryUpdateStatus(id, ArchiveJobStatus.Done, zipBytes: zipBytes));
        Assert.True(store.TryGet(id, out var job3));
        Assert.Equal(ArchiveJobStatus.Done, job3.Status);
        Assert.Null(job3.Error);
        Assert.NotNull(job3.ZipBytes);
    }

    [Fact]
    public async Task SuccessfulPath_BuildsZipAndSetsJobDone()
    {
        var root = CreateTempDir();
        try
        {
            var filesDir = Path.Combine(root, "files");
            Directory.CreateDirectory(filesDir);

            var aPath = Path.Combine(filesDir, "a.txt");
            var bPath = Path.Combine(filesDir, "b.txt");
            await File.WriteAllTextAsync(aPath, "A");
            await File.WriteAllTextAsync(bPath, "B");

            var opts = new ArchiveOptions
            {
                FilesDir = filesDir,
                MaxFilesPerArchive = 1000
            };

            var store = new InMemoryArchiveJobStore();
            var id = Guid.NewGuid();
            var requested = new List<string> { "a.txt", "b.txt" };
            var cache = new ArchiveCache(opts);
            var cacheKey = cache.GetCacheKeyForFiles(requested);
            store.Create(id, requested, cacheKey);

            Assert.True(store.TryUpdateStatus(id, ArchiveJobStatus.Processing));
            Assert.True(store.TryGet(id, out var processingJob));
            Assert.Equal(ArchiveJobStatus.Processing, processingJob.Status);

            var catalog = new PhysicalFileCatalog(opts);
            var builder = new ZipArchiveBuilder();

            var absolutePaths = await catalog.ResolveRequestedFilesAsync(processingJob.RequestedFiles, CancellationToken.None);
            var zipBytes = await builder.BuildZipAsync(absolutePaths, CancellationToken.None);
            cache.SetZipBytes(processingJob.CacheKey, zipBytes);

            Assert.True(store.TryUpdateStatus(id, ArchiveJobStatus.Done, zipBytes: zipBytes));
            Assert.True(store.TryGet(id, out var doneJob));
            Assert.Equal(ArchiveJobStatus.Done, doneJob.Status);
            Assert.Null(doneJob.Error);
            Assert.NotNull(doneJob.ZipBytes);

            using var ms = new MemoryStream(doneJob.ZipBytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);
            var names = zip.Entries.Select(e => e.FullName).ToList();
            Assert.Contains("a.txt", names);
            Assert.Contains("b.txt", names);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "kaspersky-task1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}

