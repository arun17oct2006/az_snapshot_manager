using AzureSnapshotManager.Core.Interfaces;
using AzureSnapshotManager.Core.Models;
using AzureSnapshotManager.Infrastructure.Advisory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace AzureSnapshotManager.Tests;

public class AdvisoryEngineTests
{
    private static AdvisoryEngine BuildEngine(
        out Mock<IDiscoveryEngine> discovery,
        out Mock<IPricingService> pricing,
        out Mock<ISnapshotRepository> repository)
    {
        discovery = new Mock<IDiscoveryEngine>();
        pricing = new Mock<IPricingService>();
        repository = new Mock<ISnapshotRepository>();

        return new AdvisoryEngine(discovery.Object, pricing.Object, repository.Object, NullLogger<AdvisoryEngine>.Instance);
    }

    [Fact]
    public void IsOrphaned_ReturnsTrue_WhenParentDiskIdIsNull()
    {
        var engine = BuildEngine(out _, out _, out _);
        var snapshot = new Snapshot { ParentDiskId = null };

        Assert.True(engine.IsOrphaned(snapshot, new List<ParentDisk>()));
    }

    [Fact]
    public void IsOrphaned_ReturnsFalse_WhenParentDiskStillExists()
    {
        var engine = BuildEngine(out _, out _, out _);
        var snapshot = new Snapshot { ParentDiskId = "disk-1" };
        var disks = new List<ParentDisk> { new() { DiskId = "disk-1" } };

        Assert.False(engine.IsOrphaned(snapshot, disks));
    }

    [Fact]
    public void GroupOrphans_GroupsByParentDiskId_WhenPresent()
    {
        var engine = BuildEngine(out _, out _, out _);
        var orphans = new List<Snapshot>
        {
            new() { SnapshotId = "s1", ParentDiskId = "disk-A", ResourceGroup = "rg1" },
            new() { SnapshotId = "s2", ParentDiskId = "disk-A", ResourceGroup = "rg1" },
            new() { SnapshotId = "s3", ParentDiskId = "disk-B", ResourceGroup = "rg2" },
        };

        var groups = engine.GroupOrphans(orphans);

        Assert.Equal(2, groups.Count);
        Assert.Contains(groups, g => g.GroupKey == "disk-A" && g.Snapshots.Count == 2);
        Assert.Contains(groups, g => g.GroupKey == "disk-B" && g.Snapshots.Count == 1);
    }

    [Fact]
    public void BuildPortalDeepLink_ProducesValidAzurePortalUrl()
    {
        var engine = BuildEngine(out _, out _, out _);
        var snapshot = new Snapshot
        {
            SubscriptionId = "00000000-0000-0000-0000-000000000000",
            ResourceGroup = "rg-test",
            Name = "snap-1"
        };

        var link = engine.BuildPortalDeepLink(snapshot);

        Assert.StartsWith("https://portal.azure.com/#@/resource/subscriptions/", link);
        Assert.Contains("rg-test", link);
        Assert.Contains("snap-1", link);
    }

    [Fact]
    public async Task RunAuditAsync_ExcludesSnapshots_MatchingResourceGroupRule()
    {
        var engine = BuildEngine(out var discovery, out var pricing, out var repository);

        var disks = new List<ParentDisk>(); // no live disks -> everything orphaned
        var snapshots = new List<Snapshot>
        {
            new() { SnapshotId = "s1", ResourceGroup = "rg-protected", SizeGb = 100, Location = "eastus", SkuName = "Standard_LRS" },
            new() { SnapshotId = "s2", ResourceGroup = "rg-cleanup",   SizeGb = 50,  Location = "eastus", SkuName = "Standard_LRS" },
        };

        discovery.Setup(d => d.DiscoverDisksAsync("sub-1", It.IsAny<CancellationToken>())).ReturnsAsync(disks);
        discovery.Setup(d => d.DiscoverSnapshotsAsync("sub-1", It.IsAny<CancellationToken>())).ReturnsAsync(snapshots);
        discovery.Setup(d => d.ResolveLineageAsync(snapshots, disks, It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<Snapshot> s, List<ParentDisk> _, CancellationToken _) =>
            {
                foreach (var snap in s) snap.State = SnapshotState.OrphanedSnapshot;
                return s;
            });
        pricing.Setup(p => p.PriceSnapshotsAsync(snapshots, It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Snapshot>, CancellationToken>((snaps, _) =>
            {
                foreach (var snap in snaps) snap.DailyCost = (decimal)snap.SizeGb * 0.001m;
            })
            .Returns(Task.CompletedTask);

        var preferences = new List<UserPreference>
        {
            new() { PreferenceId = "p1", SubscriptionId = "sub-1", ExclusionRule = "resourceGroup:rg-protected", IsActive = true }
        };

        var result = await engine.RunAuditAsync("sub-1", preferences);

        Assert.Single(result.OrphanedSnapshots);
        Assert.Equal("s2", result.OrphanedSnapshots[0].SnapshotId);
        repository.Verify(r => r.SaveScanLogAsync(It.IsAny<ScanLog>(), "sub-1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
