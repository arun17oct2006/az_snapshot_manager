using AzureSnapshotManager.Core.Interfaces;
using AzureSnapshotManager.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace AzureSnapshotManager.Infrastructure.Pricing;

/// <summary>
/// WBS 1.3.2 Azure Retail Prices API Integration.
/// Public, unauthenticated REST API — no credential needed.
/// Docs: https://learn.microsoft.com/rest/api/cost-management/retail-prices/azure-retail-prices
/// </summary>
public class RetailPricesApiService : IPricingService
{
    private const string BaseUrl = "https://prices.azure.com/api/retail/prices";

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RetailPricesApiService> _logger;

    public RetailPricesApiService(HttpClient httpClient, IMemoryCache cache, ILogger<RetailPricesApiService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<decimal> GetPricePerGbMonthAsync(string skuName, string location, CancellationToken ct = default)
    {
        var cacheKey = $"price:{skuName}:{location}".ToLowerInvariant();
        if (_cache.TryGetValue(cacheKey, out decimal cached))
            return cached;

        // Snapshot storage is billed per-GB/month by redundancy tier, e.g. "Standard_LRS Snapshots".
        var filter = $"armRegionName eq '{location}' and serviceName eq 'Storage' " +
                     $"and productName eq 'Standard {SkuToTier(skuName)} Snapshots' and priceType eq 'Consumption'";

        var url = $"{BaseUrl}?$filter={Uri.EscapeDataString(filter)}";

        try
        {
            var response = await _httpClient.GetFromJsonAsync<RetailPriceResponse>(url, ct);
            var price = response?.Items?.FirstOrDefault()?.RetailPrice
                        ?? GetFallbackPrice(skuName);

            _cache.Set(cacheKey, price, TimeSpan.FromHours(6));
            return price;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Retail Prices API lookup failed for {Sku}/{Location}; using fallback rate", skuName, location);
            return GetFallbackPrice(skuName);
        }
    }

    public async Task PriceSnapshotsAsync(IEnumerable<Snapshot> snapshots, CancellationToken ct = default)
    {
        // Group by (sku, location) to minimize API calls.
        var groups = snapshots.GroupBy(s => (s.SkuName, s.Location));

        foreach (var group in groups)
        {
            var pricePerGbMonth = await GetPricePerGbMonthAsync(group.Key.SkuName, group.Key.Location, ct);
            var dailyRatePerGb = pricePerGbMonth / 30m;

            foreach (var snapshot in group)
                snapshot.DailyCost = Math.Round((decimal)snapshot.SizeGb * dailyRatePerGb, 4);
        }
    }

    private static string SkuToTier(string skuName) => skuName switch
    {
        var s when s.Contains("ZRS", StringComparison.OrdinalIgnoreCase) => "ZRS",
        _ => "LRS"
    };

    /// <summary>Conservative fallback if the live API is unreachable, so audits never fail hard.</summary>
    private static decimal GetFallbackPrice(string skuName) => skuName switch
    {
        var s when s.Contains("ZRS", StringComparison.OrdinalIgnoreCase) => 0.0575m,
        _ => 0.05m // approx $/GB/month for Standard_LRS snapshot storage
    };

    private class RetailPriceResponse
    {
        public List<RetailPriceItem>? Items { get; set; }
    }

    private class RetailPriceItem
    {
        public decimal RetailPrice { get; set; }
    }
}
