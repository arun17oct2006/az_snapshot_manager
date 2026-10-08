using AzureSnapshotManager.Core.Interfaces;
using AzureSnapshotManager.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AzureSnapshotManager.Api.Controllers;

/// <summary>
/// WBS 1.5.3 User Preference Exclusions UI (API backing).
/// Use case: "Configure Retention Rules" (+extends "View Savings & Cost Metrics").
/// </summary>
[ApiController]
[Authorize]
[Route("api/subscriptions/{subscriptionId}/retention-rules")]
public class RetentionController : ControllerBase
{
    private readonly ISnapshotRepository _repository;

    public RetentionController(ISnapshotRepository repository)
    {
        _repository = repository;
    }

    [HttpGet]
    public async Task<IActionResult> GetRules(string subscriptionId, CancellationToken ct)
    {
        var rules = await _repository.GetUserPreferencesAsync(subscriptionId, ct);
        return Ok(rules);
    }

    public record CreateRuleRequest(string ExclusionRule, DateTimeOffset? KeepUntilDate);

    [HttpPost]
    public async Task<IActionResult> CreateRule(string subscriptionId, CreateRuleRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ExclusionRule))
            return BadRequest("ExclusionRule is required.");

        var preference = new UserPreference
        {
            PreferenceId = Guid.NewGuid().ToString(),
            SubscriptionId = subscriptionId,
            ExclusionRule = request.ExclusionRule,
            KeepUntilDate = request.KeepUntilDate,
            IsActive = true
        };

        await _repository.SaveUserPreferenceAsync(preference, ct);
        return CreatedAtAction(nameof(GetRules), new { subscriptionId }, preference);
    }

    [HttpDelete("{preferenceId}")]
    public async Task<IActionResult> DeleteRule(string subscriptionId, string preferenceId, CancellationToken ct)
    {
        await _repository.DeleteUserPreferenceAsync(subscriptionId, preferenceId, ct);
        return NoContent();
    }
}
