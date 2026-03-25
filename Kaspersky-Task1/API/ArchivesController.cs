using System.Threading.Channels;
using Kaspersky_Task1.Contracts;
using Kaspersky_Task1.Domain;
using Kaspersky_Task1.Services.Caches;
using Kaspersky_Task1.Services.Stores;
using Microsoft.AspNetCore.Mvc;

namespace Kaspersky_Task1.Api;

[ApiController]
[Route("api/archives")]
public sealed class ArchivesController : ControllerBase
{
    private readonly IArchiveJobStore _store;
    private readonly Channel<Guid> _queue;
    private readonly IArchiveCache _cache;
    private readonly ILogger<ArchivesController> _logger;

    public ArchivesController(
        IArchiveJobStore store,
        Channel<Guid> queue,
        IArchiveCache cache,
        ILogger<ArchivesController> logger)
    {
        _store = store;
        _queue = queue;
        _cache = cache;
        _logger = logger;
    }

    [HttpPost("init")]
    public async Task<ActionResult<InitArchiveResponse>> Init(
        [FromBody] InitArchiveRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Request: POST {Path}", HttpContext.Request.Path);
        
        var files = request.Files!
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .ToList();

        var cacheKey = _cache.GetCacheKeyForFiles(files);
        var id = Guid.NewGuid();

        _store.Create(id, files, cacheKey);
        if (_cache.TryGetZipBytes(cacheKey, out var zipBytes))
        {
            _logger.LogInformation("Archive taken from cache for process {ProcessId}", id);
            _store.TryUpdateStatus(id, ArchiveJobStatus.Done, zipBytes: zipBytes);
            return Accepted(new InitArchiveResponse(id));
        }

        await _queue.Writer.WriteAsync(id, ct);

        _logger.LogInformation("Archive job created: {ProcessId}", id);
        return Accepted(new InitArchiveResponse(id));
    }

    [HttpGet("{processId:guid}/status")]
    public ActionResult<ArchiveStatusResponse> Status(
        [FromRoute] Guid processId)
    {
        _logger.LogInformation("Request: GET {Path} {ProcessId}", HttpContext.Request.Path, processId);

        if (!_store.TryGet(processId, out var job))
            return NotFound();

        return Ok(new ArchiveStatusResponse(
            job.ProcessId,
            job.Status.ToString().ToLowerInvariant(),
            job.Error));
    }

    [HttpGet("{processId:guid}/download")]
    public ActionResult Download(
        [FromRoute] Guid processId)
    {
        _logger.LogInformation("Request: GET {Path} {ProcessId}", HttpContext.Request.Path, processId);

        if (!_store.TryGet(processId, out var job))
            return NotFound();

        if (job.Status == ArchiveJobStatus.Failed)
            return BadRequest(job.Error ?? "Archive job failed.");

        if (job.Status != ArchiveJobStatus.Done)
            return Conflict("Archive is not ready yet.");

        if (job.ZipBytes is null)
            return Conflict("Archive is not ready yet.");

        var fileName = $"archive-{processId}.zip";
        var stream = new MemoryStream(job.ZipBytes, writable: false);
        return File(stream, "application/zip", fileName);
    }
}

