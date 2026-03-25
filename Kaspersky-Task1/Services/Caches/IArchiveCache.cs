namespace Kaspersky_Task1.Services.Caches;

public interface IArchiveCache
{
    string GetCacheKeyForFiles(IEnumerable<string> requestedFileNames);
    bool TryGetZipBytes(string cacheKey, out byte[] zipBytes);
    void SetZipBytes(string cacheKey, byte[] zipBytes);
}

