namespace AzureSnapshotManager.Core.Models;

/// <summary>
/// Maps to ER entity "Snapshot" ("Is A" -> Active | Orphaned).
/// </summary>
public enum SnapshotState
{
    ActiveSnapshot,
    OrphanedSnapshot
}

/// <summary>
/// Maps to ER entity "Snapshot" with attributes Snapshot ID, Size GB,
/// Time Created, Daily Cost. Produced by the Discovery Engine (WBS 1.3.1)
/// and priced by the Pricing API integration (WBS 1.3.2).
/// </summary>
public class Snapshot
{
    public string SnapshotId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string SkuName { get; set; } = "Standard_LRS";

    public double SizeGb { get; set; }
    public DateTimeOffset TimeCreated { get; set; }
    public decimal DailyCost { get; set; }

    /// <summary>Foreign key back to the Parent Disk ("Originate").</summary>
    public string? ParentDiskId { get; set; }

    /// <summary>"Is A" relationship: Active vs Orphaned.</summary>
    public SnapshotState State { get; set; } = SnapshotState.OrphanedSnapshot;

    /// <summary>"Override" relationship to a UserPreference exclusion rule, if any.</summary>
    public string? OverridingPreferenceId { get; set; }

    // "Evaluate" relationship in the ER diagram
    public List<ScanLog> ScanHistory { get; set; } = new();
}
