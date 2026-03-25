using Kaspersky_Task1.Services.Catalogs;
using Microsoft.AspNetCore.Mvc;

namespace Kaspersky_Task1.Api;

[ApiController]
[Route("api/files")]
public sealed class FilesController : ControllerBase
{
    private readonly IFileCatalog _catalog;
    private readonly ILogger<FilesController> _logger;

    public FilesController(IFileCatalog catalog, ILogger<FilesController> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<string>>> GetFiles(CancellationToken ct)
    {
        _logger.LogInformation("Request: GET {Path}", HttpContext.Request.Path);

        var files = await _catalog.ListFileNamesAsync(ct);
        return Ok(files);
    }
}

