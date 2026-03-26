using System.Threading.Channels;
using Kaspersky_Task1.Domain;
using Kaspersky_Task1.Services.Builders;
using Kaspersky_Task1.Services.Caches;
using Kaspersky_Task1.Services.Catalogs;
using Kaspersky_Task1.Services.Stores;

namespace Kaspersky_Task1.Background;

public sealed class ArchiveBackgroundWorker : BackgroundService
{
    private readonly Channel<Guid> _queue;
    private readonly IArchiveJobStore _store;
    private readonly IFileCatalog _fileCatalog;
    private readonly IArchiveBuilder _zipBuilder;
    private readonly IArchiveCache _cache;
    private readonly ILogger<ArchiveBackgroundWorker> _logger;

    public ArchiveBackgroundWorker(
        Channel<Guid> queue,
        IArchiveJobStore store,
        IFileCatalog fileCatalog,
        IArchiveBuilder zipBuilder,
        IArchiveCache cache,
        ILogger<ArchiveBackgroundWorker> logger)
    {
        _queue = queue;
        _store = store;
        _fileCatalog = fileCatalog;
        _zipBuilder = zipBuilder;
        _cache = cache;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var id in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                if (!_store.TryGet(id, out var job))
                    continue;

                _store.TryUpdateStatus(id, ArchiveJobStatus.Processing);

                var distinctFiles = job.RequestedFiles
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (_cache.TryGetZipBytes(job.CacheKey, out var zipBytesFromCache))
                {
                    _logger.LogInformation("Archive taken from cache for process {ProcessId}", id);
                    _store.TryUpdateStatus(id, ArchiveJobStatus.Done, zipBytes: zipBytesFromCache);
                    continue;
                }

                var absolutePaths = await _fileCatalog.ResolveRequestedFilesAsync(distinctFiles, stoppingToken);
                var zipBytes = await _zipBuilder.BuildZipAsync(absolutePaths, stoppingToken);
                _cache.SetZipBytes(job.CacheKey, zipBytes);
                _store.TryUpdateStatus(id, ArchiveJobStatus.Done, zipBytes: zipBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Archive job {ProcessId} failed", id);
                _store.TryUpdateStatus(id, ArchiveJobStatus.Failed, error: ex.Message);
            }
        }
    }
}

