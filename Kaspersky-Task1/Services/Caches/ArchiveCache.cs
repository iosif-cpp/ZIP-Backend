using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Kaspersky_Task1.Configuration;

namespace Kaspersky_Task1.Services.Caches;

public sealed class ArchiveCache : IArchiveCache, IDisposable
{
    private sealed class CacheEntry
    {
        public byte[] ZipBytes { get; init; } = Array.Empty<byte>();
        public DateTimeOffset ExpiresAt { get; init; }
    }

    private readonly ArchiveOptions _opts;
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public ArchiveCache(ArchiveOptions opts)
    {
        _opts = opts;
        _cleanupTimer = new Timer(_ => CleanupExpired(), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
    }

    public string GetCacheKeyForFiles(IEnumerable<string> requestedFileNames) => ComputeCacheKey(requestedFileNames);

    public bool TryGetZipBytes(string cacheKey, out byte[] zipBytes)
    {
        zipBytes = Array.Empty<byte>();
        if (!_entries.TryGetValue(cacheKey, out var entry))
            return false;

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _entries.TryRemove(cacheKey, out _);
            return false;
        }

        zipBytes = entry.ZipBytes;
        return true;
    }

    public void SetZipBytes(string cacheKey, byte[] zipBytes)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_opts.ArchiveCacheTtlMinutes);
        _entries[cacheKey] = new CacheEntry { ZipBytes = zipBytes, ExpiresAt = expiresAt };
    }

    private void CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in _entries)
        {
            if (pair.Value.ExpiresAt <= now)
                _entries.TryRemove(pair.Key, out _);
        }
    }

    private static string NormalizeAndBuildKey(IEnumerable<string> requestedFileNames)
    {
        var normalized = requestedFileNames
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        return string.Join("|", normalized);
    }

    private static string ComputeSha256Hex(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeCacheKey(IEnumerable<string> requestedFileNames)
    {
        var keyInput = NormalizeAndBuildKey(requestedFileNames);
        return ComputeSha256Hex(keyInput);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cleanupTimer.Dispose();
    }
}

