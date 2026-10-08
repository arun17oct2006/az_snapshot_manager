namespace AzureSnapshotManager.Core.Models;

/// <summary>
/// Maps to ER entity "Subscription". A tenant's Azure subscription that is
/// onboarded for snapshot discovery and advisory scanning (WBS 1.2.1).
/// </summary>
public class Subscription
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;

    // 1 subscription -> many parent disks ("Contains" relationship in the ER diagram)
    public List<ParentDisk> Disks { get; set; } = new();
}
