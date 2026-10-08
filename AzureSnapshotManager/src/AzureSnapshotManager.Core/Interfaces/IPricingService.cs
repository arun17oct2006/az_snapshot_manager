using AzureSnapshotManager.Core.Models;

namespace AzureSnapshotManager.Core.Interfaces;

/// <summary>
/// WBS 1.3.2 Azure Retail Prices API Integration.
/// "Calculate Cost" relationship in the ER diagram (Pricing API -> Snapshot).
/// </summary>
public interface IPricingService
{
    /// <summary>Returns price-per-GB-per-month for a given SKU + region, from the Retail Prices API.</summary>
    Task<decimal> GetPricePerGbMonthAsync(string skuName, string location, CancellationToken ct = default);

    /// <summary>Convenience helper: applies pricing to a batch of snapshots and sets DailyCost.</summary>
    Task PriceSnapshotsAsync(IEnumerable<Snapshot> snapshots, CancellationToken ct = default);
}
