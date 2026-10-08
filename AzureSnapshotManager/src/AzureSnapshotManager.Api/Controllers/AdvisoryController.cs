using AzureSnapshotManager.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzureSnapshotManager.Api.Controllers;

/// <summary>
/// Use case: "Trigger Advisory Audit" (+include "View Savings & Cost Metrics").
/// </summary>
[ApiController]
[Authorize]
[Route("api/subscriptions/{subscriptionId}/audit")]
public class AdvisoryController : ControllerBase
{
    private readonly IAdvisoryEngine _advisoryEngine;
    private readonly ISnapshotRepository _repository;

    public AdvisoryController(IAdvisoryEngine advisoryEngine, ISnapshotRepository repository)
    {
        _advisoryEngine = advisoryEngine;
        _repository = repository;
    }

    /// <summary>Runs a full discovery + pricing + retention-aware advisory audit for a subscription.</summary>
    [HttpPost("run")]
    public async Task<IActionResult> RunAudit(string subscriptionId, CancellationToken ct)
    {
        var preferences = await _repository.GetUserPreferencesAsync(subscriptionId, ct);
        var result = await _advisoryEngine.RunAuditAsync(subscriptionId, preferences, ct);

        return Ok(new
        {
            result.SubscriptionId,
            result.ScanTimestamp,
            OrphanedCount = result.OrphanedSnapshots.Count,
            result.TotalDailyWaste,
            result.TotalMonthlyWaste,
            GroupCount = result.Groups.Count,
            result.Groups
        });
    }
}
