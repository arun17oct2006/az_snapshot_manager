using AzureSnapshotManager.Core.Interfaces;
using AzureSnapshotManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace AzureSnapshotManager.Infrastructure.Advisory;

/// <summary>
/// WBS 1.4 Advisory Analytics Engine.
/// 1.4.1 Single-Instance Cleanup Engine, 1.4.2 Multi-Resource Grouping Logic,
/// 1.4.3 Portal Deep Link Generator.
/// </summary>
public class AdvisoryEngine : IAdvisoryEngine
{
    private readonly IDiscoveryEngine _discoveryEngine;
    private readonly IPricingService _pricingService;
    private readonly ISnapshotRepository _repository;
    private readonly ILogger<AdvisoryEngine> _logger;

    public AdvisoryEngine(
        IDiscoveryEngine discoveryEngine,
        IPricingService pricingService,
        ISnapshotRepository repository,
        ILogger<AdvisoryEngine> logger)
    {
        _discoveryEngine = discoveryEngine;
        _pricingService = pricingService;
        _repository = repository;
        _logger = logger;
    }

    /// <summary>1.4.1 Single-Instance Cleanup Engine.</summary>
    public bool IsOrphaned(Snapshot snapshot, List<ParentDisk> knownDisks)
    {
        if (string.IsNullOrEmpty(snapshot.ParentDiskId))
            return true; // never had a resolvable parent

        var parentExists = knownDisks.Any(d =>
            string.Equals(d.DiskId, snapshot.ParentDiskId, StringComparison.OrdinalIgnoreCase));

        return !parentExists;
    }

    /// <summary>1.4.2 Multi-Resource Grouping Logic — clusters orphans by parent disk, else by resource group.</summary>
    public List<SnapshotGroup> GroupOrphans(List<Snapshot> orphanedSnapshots)
    {
        var byParent = orphanedSnapshots
            .Where(s => !string.IsNullOrEmpty(s.ParentDiskId))
            .GroupBy(s => s.ParentDiskId!)
            .Select(g => new SnapshotGroup
            {
                GroupKey = g.Key,
                GroupReason = "Shared parent disk (deleted)",
                Snapshots = g.ToList()
            });

        var orphanedWithoutParent = orphanedSnapshots
            .Where(s => string.IsNullOrEmpty(s.ParentDiskId))
            .GroupBy(s => s.ResourceGroup)
            .Select(g => new SnapshotGroup
            {
                GroupKey = g.Key,
                GroupReason = "No traceable parent disk — grouped by resource group",
                Snapshots = g.ToList()
            });

        return byParent.Concat(orphanedWithoutParent).ToList();
    }

    /// <summary>
    /// Full audit pipeline for "Trigger Advisory Audit" (+includes "View Savings
    /// &amp; Cost Metrics"), honoring UserPreference exclusion rules
    /// ("Configure Retention Rules" +extends "View Savings &amp; Cost Metrics").
    /// </summary>
    public async Task<AdvisoryResult> RunAuditAsync(
        string subscriptionId,
        List<UserPreference> preferences,
        CancellationToken ct = default)
    {
        var disks = await _discoveryEngine.DiscoverDisksAsync(subscriptionId, ct);
        var snapshots = await _discoveryEngine.DiscoverSnapshotsAsync(subscriptionId, ct);
        snapshots = await _discoveryEngine.ResolveLineageAsync(snapshots, disks, ct);

        await _pricingService.PriceSnapshotsAsync(snapshots, ct);

        var orphaned = snapshots.Where(s => s.State == SnapshotState.OrphanedSnapshot).ToList();
        var (kept, excluded) = ApplyRetentionRules(orphaned, preferences);

        var result = new AdvisoryResult
        {
            SubscriptionId = subscriptionId,
            ScanTimestamp = DateTimeOffset.UtcNow,
            OrphanedSnapshots = kept,
            Groups = GroupOrphans(kept)
        };

        foreach (var snapshot in kept)
        {
            var log = new ScanLog
            {
                Timestamp = result.ScanTimestamp,
                SnapshotId = snapshot.SnapshotId,
                CalculatedSavings = snapshot.DailyCost * 30,
                Reason = "Orphaned snapshot — no live parent disk found"
            };
            await _repository.SaveScanLogAsync(log, subscriptionId, ct);
        }

        _logger.LogInformation(
            "Audit complete for {SubscriptionId}: {OrphanCount} orphaned snapshots flagged, {ExcludedCount} excluded by retention rules, ${Savings:0.00}/mo potential savings",
            subscriptionId, kept.Count, excluded.Count, result.TotalMonthlyWaste);

        return result;
    }

    /// <summary>
    /// Excludes snapshots matching an active UserPreference exclusion rule or
    /// still within their Keep Until Date ("Override" relationship in the ER diagram).
    /// </summary>
    private static (List<Snapshot> kept, List<Snapshot> excluded) ApplyRetentionRules(
        List<Snapshot> orphaned, List<UserPreference> preferences)
    {
        var kept = new List<Snapshot>();
        var excluded = new List<Snapshot>();
        var activeRules = preferences.Where(p => p.IsActive).ToList();

        foreach (var snapshot in orphaned)
        {
            var matchingRule = activeRules.FirstOrDefault(rule => MatchesRule(snapshot, rule));

            if (matchingRule is not null)
            {
                snapshot.OverridingPreferenceId = matchingRule.PreferenceId;
                excluded.Add(snapshot);
            }
            else
            {
                kept.Add(snapshot);
            }
        }

        return (kept, excluded);
    }

    private static bool MatchesRule(Snapshot snapshot, UserPreference rule)
    {
        if (rule.KeepUntilDate.HasValue && rule.KeepUntilDate.Value > DateTimeOffset.UtcNow
            && snapshot.Name.Contains(rule.ExclusionRule, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Supports simple "resourceGroup:<name>" and "snapshotName:contains:<text>" rule syntax.
        var parts = rule.ExclusionRule.Split(':', 3, StringSplitOptions.TrimEntries);

        return parts[0].ToLowerInvariant() switch
        {
            "resourcegroup" when parts.Length >= 2 =>
                string.Equals(snapshot.ResourceGroup, parts[1], StringComparison.OrdinalIgnoreCase),
            "snapshotname" when parts.Length >= 3 && parts[1].Equals("contains", StringComparison.OrdinalIgnoreCase) =>
                snapshot.Name.Contains(parts[2], StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    /// <summary>1.4.3 Portal Deep Link Generator.</summary>
    public string BuildPortalDeepLink(Snapshot snapshot)
    {
        var resourceId = $"/subscriptions/{snapshot.SubscriptionId}/resourceGroups/{snapshot.ResourceGroup}" +
                          $"/providers/Microsoft.Compute/snapshots/{snapshot.Name}";

        return $"https://portal.azure.com/#@/resource{resourceId}/overview";
    }
}
