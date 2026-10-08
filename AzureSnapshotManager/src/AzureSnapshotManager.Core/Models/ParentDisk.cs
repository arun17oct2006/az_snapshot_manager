namespace AzureSnapshotManager.Core.Models;

/// <summary>
/// Maps to ER entity "Parent Disk". A managed disk that "Originates" one or
/// more snapshots. Discovered via Azure Resource Graph (WBS 1.3.1).
/// </summary>
public class ParentDisk
{
    public string DiskId { get; set; } = string.Empty;
    public string DiskName { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;

    /// <summary>True if the disk itself still exists / is attached to a VM.</summary>
    public bool Exists { get; set; }

    // "Originate" relationship in the ER diagram
    public List<Snapshot> Snapshots { get; set; } = new();
}
