namespace AzureSnapshotManager.Core.Models;

/// <summary>
/// Maps to ER entity "User Preference" (Preference ID, Exclusion Rule,
/// Keep Until Date). Configured via the Retention Rules UI (WBS 1.5.3)
/// and satisfies the "Configure Retention Rules" use case, which +extends
/// "View Savings &amp; Cost Metrics".
/// </summary>
public class UserPreference
{
    public string PreferenceId { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;

    /// <summary>
    /// Simple rule expression, e.g. "tag:environment=prod" or
    /// "resourceGroup:rg-critical" or "snapshotName:contains:backup-".
    /// </summary>
    public string ExclusionRule { get; set; } = string.Empty;

    public DateTimeOffset? KeepUntilDate { get; set; }

    public bool IsActive { get; set; } = true;
}
