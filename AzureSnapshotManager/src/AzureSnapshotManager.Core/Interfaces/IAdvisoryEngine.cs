using AzureSnapshotManager.Core.Models;

namespace AzureSnapshotManager.Core.Interfaces;

/// <summary>
/// WBS 1.4 Advisory Analytics Engine.
/// Backs use cases "Trigger Advisory Audit" (includes) and
/// "View Savings &amp; Cost Metrics" (extended by "Configure Retention Rules").
/// </summary>
public interface IAdvisoryEngine
{
    /// <summary>1.4.1 — Single-instance cleanup check: is this one snapshot orphaned/wasteful?</summary>
    bool IsOrphaned(Snapshot snapshot, List<ParentDisk> knownDisks);

    /// <summary>1.4.2 — Multi-resource grouping: cluster orphaned snapshots by parent disk / resource group.</summary>
    List<SnapshotGroup> GroupOrphans(List<Snapshot> orphanedSnapshots);

    /// <summary>
    /// Runs a full audit for a subscription: discover -> price -> apply user
    /// preferences (exclusion rules / keep-until dates) -> group -> log.
    /// </summary>
    Task<AdvisoryResult> RunAuditAsync(
        string subscriptionId,
        List<UserPreference> preferences,
        CancellationToken ct = default);

    /// <summary>1.4.3 — Build an Azure Portal deep link for a given resource (for one-click remediation).</summary>
    string BuildPortalDeepLink(Snapshot snapshot);
}
