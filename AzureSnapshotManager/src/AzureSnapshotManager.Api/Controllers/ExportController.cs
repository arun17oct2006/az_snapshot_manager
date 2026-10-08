using AzureSnapshotManager.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzureSnapshotManager.Api.Controllers;

/// <summary>Use case: "Export Reports & Portal Deep Links".</summary>
[ApiController]
[Authorize]
[Route("api/subscriptions/{subscriptionId}/export")]
public class ExportController : ControllerBase
{
    private readonly IAdvisoryEngine _advisoryEngine;
    private readonly IExportService _exportService;
    private readonly ISnapshotRepository _repository;

    public ExportController(
        IAdvisoryEngine advisoryEngine,
        IExportService exportService,
        ISnapshotRepository repository)
    {
        _advisoryEngine = advisoryEngine;
        _exportService = exportService;
        _repository = repository;
    }

    /// <summary>Re-runs an audit and returns it as a downloadable CSV with Azure Portal deep links.</summary>
    [HttpGet("csv")]
    public async Task<IActionResult> ExportCsv(string subscriptionId, CancellationToken ct)
    {
        var preferences = await _repository.GetUserPreferencesAsync(subscriptionId, ct);
        var result = await _advisoryEngine.RunAuditAsync(subscriptionId, preferences, ct);

        var csvBytes = _exportService.ExportToCsv(result, _advisoryEngine.BuildPortalDeepLink);
        var fileName = $"snapshot-advisory-{subscriptionId}-{DateTime.UtcNow:yyyyMMdd}.csv";

        return File(csvBytes, "text/csv", fileName);
    }
}
