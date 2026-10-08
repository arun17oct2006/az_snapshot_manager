using AzureSnapshotManager.Core.Interfaces;
using AzureSnapshotManager.Core.Models;
using AzureSnapshotManager.Infrastructure.Security;
using Azure.ResourceManager;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AzureSnapshotManager.Infrastructure.Discovery;

/// <summary>
/// WBS 1.3.1 Azure Resource Graph (ARG) Engine.
/// Implements "Trigger Advisory Audit" -> queries Azure Resource Graph
/// (use case diagram, right-hand actor "Azure Resource Graph").
/// </summary>
public class ResourceGraphDiscoveryEngine : IDiscoveryEngine
{
    private readonly ArmClient _armClient;
    private readonly ILogger<ResourceGraphDiscoveryEngine> _logger;

    private const string SnapshotQuery = @"
        Resources
        | where type =~ 'microsoft.compute/snapshots'
        | project snapshotId = id, name, resourceGroup, subscriptionId,
                  location, skuName = sku.name,
                  sizeGb = toint(properties.diskSizeGB),
                  timeCreated = properties.timeCreated,
                  parentDiskId = tostring(properties.creationData.sourceResourceId)";

    private const string DiskQuery = @"
        Resources
        | where type =~ 'microsoft.compute/disks'
        | project diskId = id, diskName = name, resourceGroup, subscriptionId";

    public ResourceGraphDiscoveryEngine(
        ManagedIdentityAuthProvider authProvider,
        ILogger<ResourceGraphDiscoveryEngine> logger)
    {
        _armClient = new ArmClient(authProvider.GetCredential());
        _logger = logger;
    }

    public async Task<List<Snapshot>> DiscoverSnapshotsAsync(string subscriptionId, CancellationToken ct = default)
    {
        var rows = await RunQueryAsync(subscriptionId, SnapshotQuery, ct);
        var results = new List<Snapshot>();

        foreach (var row in rows)
        {
            results.Add(new Snapshot
            {
                SnapshotId = GetString(row, "snapshotId"),
                Name = GetString(row, "name"),
                ResourceGroup = GetString(row, "resourceGroup"),
                SubscriptionId = GetString(row, "subscriptionId"),
                Location = GetString(row, "location"),
                SkuName = GetString(row, "skuName", "Standard_LRS"),
                SizeGb = GetDouble(row, "sizeGb"),
                TimeCreated = GetDateTimeOffset(row, "timeCreated"),
                ParentDiskId = GetString(row, "parentDiskId", string.Empty) switch
                {
                    "" => null,
                    var v => v
                }
            });
        }

        _logger.LogInformation("Discovered {Count} snapshots in subscription {SubscriptionId}", results.Count, subscriptionId);
        return results;
    }

    public async Task<List<ParentDisk>> DiscoverDisksAsync(string subscriptionId, CancellationToken ct = default)
    {
        var rows = await RunQueryAsync(subscriptionId, DiskQuery, ct);
        var results = new List<ParentDisk>();

        foreach (var row in rows)
        {
            results.Add(new ParentDisk
            {
                DiskId = GetString(row, "diskId"),
                DiskName = GetString(row, "diskName"),
                ResourceGroup = GetString(row, "resourceGroup"),
                SubscriptionId = GetString(row, "subscriptionId"),
                Exists = true
            });
        }

        _logger.LogInformation("Discovered {Count} managed disks in subscription {SubscriptionId}", results.Count, subscriptionId);
        return results;
    }

    /// <summary>1.3.3 Lineage &amp; Orphan Disk Tracker.</summary>
    public Task<List<Snapshot>> ResolveLineageAsync(
        List<Snapshot> snapshots,
        List<ParentDisk> disks,
        CancellationToken ct = default)
    {
        var liveDiskIds = disks.Select(d => d.DiskId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var snapshot in snapshots)
        {
            var hasLiveParent = snapshot.ParentDiskId is not null
                                 && liveDiskIds.Contains(snapshot.ParentDiskId);

            snapshot.State = hasLiveParent
                ? SnapshotState.ActiveSnapshot
                : SnapshotState.OrphanedSnapshot;
        }

        return Task.FromResult(snapshots);
    }

    private async Task<List<Dictionary<string, JsonElement>>> RunQueryAsync(
        string subscriptionId, string query, CancellationToken ct)
    {
        var content = new ResourceQueryContent(query)
        {
            Subscriptions = { subscriptionId }
        };

        Azure.Response<ResourceQueryResult> response;
        try
        {
            var tenant = _armClient.GetTenants().First();
            response = await tenant.GetResourcesAsync(content, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Azure Resource Graph query failed for subscription {SubscriptionId}. " +
                "Common causes: not signed in (run 'az login'), the signed-in account lacks " +
                "Reader access on this subscription, or the subscription ID is wrong.",
                subscriptionId);
            throw;
        }

        var results = new List<Dictionary<string, JsonElement>>();
        if (response.Value.Data is BinaryData data)
        {
            var doc = JsonDocument.Parse(data);
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var row = new Dictionary<string, JsonElement>();
                foreach (var prop in element.EnumerateObject())
                    row[prop.Name] = prop.Value;
                results.Add(row);
            }
        }

        return results;
    }

    private static string GetString(Dictionary<string, JsonElement> row, string key, string fallback = "")
        => row.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;

    private static double GetDouble(Dictionary<string, JsonElement> row, string key)
        => row.TryGetValue(key, out var v) && v.TryGetDouble(out var d) ? d : 0;

    private static DateTimeOffset GetDateTimeOffset(Dictionary<string, JsonElement> row, string key)
        => row.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String
           && DateTimeOffset.TryParse(v.GetString(), out var dt) ? dt : DateTimeOffset.MinValue;
}
