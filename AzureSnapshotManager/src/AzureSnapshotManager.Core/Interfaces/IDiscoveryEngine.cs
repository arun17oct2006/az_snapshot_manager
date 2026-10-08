using AzureSnapshotManager.Core.Models;

namespace AzureSnapshotManager.Core.Interfaces;

/// <summary>
/// WBS 1.3 Inventory &amp; Discovery Engine.
/// Backs use case "Trigger Advisory Audit" -> includes querying Azure Resource Graph.
/// </summary>
public interface IDiscoveryEngine
{
    /// <summary>1.3.1 — Query Azure Resource Graph (ARG) for all disk snapshots in scope.</summary>
    Task<List<Snapshot>> DiscoverSnapshotsAsync(string subscriptionId, CancellationToken ct = default);

    /// <summary>1.3.1 — Query ARG for all managed disks in scope (used to resolve parent/orphan state).</summary>
    Task<List<ParentDisk>> DiscoverDisksAsync(string subscriptionId, CancellationToken ct = default);

    /// <summary>1.3.3 — Resolve snapshot -> parent disk lineage and flag orphans (no live parent disk).</summary>
    Task<List<Snapshot>> ResolveLineageAsync(
        List<Snapshot> snapshots,
        List<ParentDisk> disks,
        CancellationToken ct = default);
}
