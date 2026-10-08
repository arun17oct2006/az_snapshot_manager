namespace AzureSnapshotManager.Core.Models;

/// <summary>Result of running the advisory engine (WBS 1.4) over a subscription.</summary>
public class AdvisoryResult
{
    public string SubscriptionId { get; set; } = string.Empty;
    public DateTimeOffset ScanTimestamp { get; set; } = DateTimeOffset.UtcNow;
    public List<Snapshot> OrphanedSnapshots { get; set; } = new();
    public List<SnapshotGroup> Groups { get; set; } = new();
    public decimal TotalDailyWaste => OrphanedSnapshots.Sum(s => s.DailyCost);
    public decimal TotalMonthlyWaste => TotalDailyWaste * 30;
}

/// <summary>
/// A cluster of related orphaned snapshots, e.g. all snapshots from the same
/// deleted parent disk or the same resource group (WBS 1.4.2 Multi-Resource
/// Grouping Logic).
/// </summary>
public class SnapshotGroup
{
    public string GroupKey { get; set; } = string.Empty;   // e.g. resource group or parent disk id
    public string GroupReason { get; set; } = string.Empty; // e.g. "Parent disk deleted"
    public List<Snapshot> Snapshots { get; set; } = new();
    public decimal GroupDailyWaste => Snapshots.Sum(s => s.DailyCost);
}
