using System.Net.Http.Headers;
using System.Net.Http.Json;
using AzureSnapshotManager.Infrastructure.Security;
using Microsoft.Extensions.Logging;

namespace AzureSnapshotManager.Infrastructure.Billing;

/// <summary>
/// WBS 1.1.3 Partner Center Metered Billing.
///
/// Sends usage events to the Marketplace Metering API so this offer can bill
/// customers per audit / per GB-of-waste-detected, on top of (or instead of)
/// a flat SaaS plan. Requires the SaaS subscription's resourceId/planId,
/// captured during the Marketplace "Resolve" + landing-page webhook flow —
/// that fulfillment handshake is offer-specific and NOT included here; wire
/// this service up once you have those values persisted per tenant.
///
/// Docs: https://learn.microsoft.com/en-us/partner-center/marketplace-offers/marketplace-metering-service-apis
/// </summary>
public class MarketplaceMeteringService
{
    private const string MeteringApiUrl = "https://marketplaceapi.microsoft.com/api/usageEvent?api-version=2018-08-31";

    private readonly HttpClient _httpClient;
    private readonly ManagedIdentityAuthProvider _authProvider;
    private readonly ILogger<MarketplaceMeteringService> _logger;

    public MarketplaceMeteringService(
        HttpClient httpClient,
        ManagedIdentityAuthProvider authProvider,
        ILogger<MarketplaceMeteringService> logger)
    {
        _httpClient = httpClient;
        _authProvider = authProvider;
        _logger = logger;
    }

    /// <param name="resourceId">The Marketplace SaaS subscription id captured at fulfillment time.</param>
    /// <param name="dimension">Billing dimension defined in Partner Center, e.g. "audit_run".</param>
    /// <param name="quantity">Units to bill for this event.</param>
    public async Task EmitUsageEventAsync(
        string resourceId, string dimension, double quantity, CancellationToken ct = default)
    {
        // The Marketplace Metering API is authenticated with an AAD token for
        // resource https://marketplaceapi.microsoft.com, obtainable via the
        // same Managed Identity used elsewhere in this app.
        var tokenRequest = new Azure.Core.TokenRequestContext(
            new[] { "20e940b3-4c77-4b0b-9a53-9e16a1b010a7/.default" });
        var token = await _authProvider.GetCredential().GetTokenAsync(tokenRequest, ct);

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token.Token);

        var payload = new
        {
            resourceId,
            quantity,
            dimension,
            effectiveStartTime = DateTimeOffset.UtcNow.ToString("o"),
            planId = "azure-snapshot-manager-plan"
        };

        var response = await _httpClient.PostAsJsonAsync(MeteringApiUrl, payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Marketplace usage event failed ({Status}): {Body}", response.StatusCode, body);
        }
        else
        {
            _logger.LogInformation("Emitted Marketplace usage event: {Dimension} x{Quantity} for {ResourceId}", dimension, quantity, resourceId);
        }
    }
}
