using AzureSnapshotManager.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzureSnapshotManager.Api.Controllers;

/// <summary>
/// WBS 1.5.1 Azure Workbooks Dashboard (API backing).
/// [Authorize] enforces the "Authenticate & Access Dashboard" use case via
/// Microsoft Entra ID (configured in Program.cs); every route below is only
/// reachable by an authenticated Cloud Administrator / DevOps / FinOps Analyst.
/// </summary>
[ApiController]
[Authorize]
[Route("api/subscriptions/{subscriptionId}/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IAdvisoryEngine _advisoryEngine;
    private readonly ISnapshotRepository _repository;

    public DashboardController(IAdvisoryEngine advisoryEngine, ISnapshotRepository repository)
    {
        _advisoryEngine = advisoryEngine;
        _repository = repository;
    }

    /// <summary>Use case: "View Savings & Cost Metrics" — returns the most recent scan log summary.</summary>
    [HttpGet("savings")]
    public async Task<IActionResult> GetSavingsSummary(string subscriptionId, CancellationToken ct)
    {
        var logs = await _repository.GetScanLogsAsync(subscriptionId, ct);

        var summary = new
        {
            SubscriptionId = subscriptionId,
            TotalFlaggedSnapshots = logs.Count,
            TotalMonthlySavings = logs.Sum(l => l.CalculatedSavings),
            LastScan = logs.OrderByDescending(l => l.Timestamp).FirstOrDefault()?.Timestamp
        };

        return Ok(summary);
    }

    /// <summary>Raw scan history, e.g. for an Azure Workbook data source.</summary>
    [HttpGet("scan-history")]
    public async Task<IActionResult> GetScanHistory(string subscriptionId, CancellationToken ct)
    {
        var logs = await _repository.GetScanLogsAsync(subscriptionId, ct);
        return Ok(logs.OrderByDescending(l => l.Timestamp));
    }
}
