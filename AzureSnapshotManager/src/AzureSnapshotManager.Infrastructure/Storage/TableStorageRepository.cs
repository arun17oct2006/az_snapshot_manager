using AzureSnapshotManager.Core.Interfaces;
using AzureSnapshotManager.Core.Models;
using AzureSnapshotManager.Infrastructure.Security;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AzureSnapshotManager.Infrastructure.Storage;

/// <summary>
/// WBS 1.2.2 Configure Azure Table Storage, 1.2.3 Enforce Customer Data Boundary.
///
/// Data boundary rule: PartitionKey is ALWAYS the caller-supplied
/// subscriptionId (tenant id). Every query is scoped with
/// `PartitionKey eq '{subscriptionId}'`, so one customer's queries can never
/// return another customer's rows, even by accident — this is enforced here
/// in one place rather than trusted to every caller.
/// </summary>
public class TableStorageRepository : ISnapshotRepository
{
    private const string ScanLogTable = "ScanLogs";
    private const string PreferenceTable = "UserPreferences";

    private readonly TableServiceClient _serviceClient;
    private readonly ILogger<TableStorageRepository> _logger;

    public TableStorageRepository(
        IConfiguration configuration,
        ManagedIdentityAuthProvider authProvider,
        ILogger<TableStorageRepository> logger)
    {
        var tableEndpoint = configuration["Azure:TableStorageEndpoint"]
            ?? throw new InvalidOperationException("Azure:TableStorageEndpoint is not configured.");

        // Uses the same Managed Identity as the rest of the app (WBS 1.6.1),
        // scoped down via RBAC to Storage Table Data Contributor only (WBS 1.6.2).
        _serviceClient = new TableServiceClient(new Uri(tableEndpoint), authProvider.GetCredential());
        _logger = logger;
    }

    public async Task SaveScanLogAsync(ScanLog log, string subscriptionId, CancellationToken ct = default)
    {
        var client = await GetTableClientAsync(ScanLogTable, ct);

        var entity = new TableEntity(subscriptionId, log.ScanId)
        {
            { nameof(ScanLog.Timestamp), log.Timestamp },
            { nameof(ScanLog.SnapshotId), log.SnapshotId },
            { nameof(ScanLog.CalculatedSavings), (double)log.CalculatedSavings },
            { nameof(ScanLog.Reason), log.Reason }
        };

        await client.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    public async Task<List<ScanLog>> GetScanLogsAsync(string subscriptionId, CancellationToken ct = default)
    {
        var client = await GetTableClientAsync(ScanLogTable, ct);
        var logs = new List<ScanLog>();

        // PartitionKey filter == the data-boundary enforcement point.
        await foreach (var entity in client.QueryAsync<TableEntity>(
            filter: $"PartitionKey eq '{Escape(subscriptionId)}'", cancellationToken: ct))
        {
            logs.Add(new ScanLog
            {
                ScanId = entity.RowKey,
                Timestamp = entity.GetDateTimeOffset(nameof(ScanLog.Timestamp)) ?? DateTimeOffset.MinValue,
                SnapshotId = entity.GetString(nameof(ScanLog.SnapshotId)) ?? string.Empty,
                CalculatedSavings = (decimal)(entity.GetDouble(nameof(ScanLog.CalculatedSavings)) ?? 0),
                Reason = entity.GetString(nameof(ScanLog.Reason)) ?? string.Empty
            });
        }

        return logs;
    }

    public async Task SaveUserPreferenceAsync(UserPreference preference, CancellationToken ct = default)
    {
        var client = await GetTableClientAsync(PreferenceTable, ct);

        var entity = new TableEntity(preference.SubscriptionId, preference.PreferenceId)
        {
            { nameof(UserPreference.ExclusionRule), preference.ExclusionRule },
            { nameof(UserPreference.KeepUntilDate), preference.KeepUntilDate },
            { nameof(UserPreference.IsActive), preference.IsActive }
        };

        await client.UpsertEntityAsync(entity, TableUpdateMode.Replace, ct);
    }

    public async Task<List<UserPreference>> GetUserPreferencesAsync(string subscriptionId, CancellationToken ct = default)
    {
        var client = await GetTableClientAsync(PreferenceTable, ct);
        var preferences = new List<UserPreference>();

        await foreach (var entity in client.QueryAsync<TableEntity>(
            filter: $"PartitionKey eq '{Escape(subscriptionId)}'", cancellationToken: ct))
        {
            preferences.Add(new UserPreference
            {
                PreferenceId = entity.RowKey,
                SubscriptionId = subscriptionId,
                ExclusionRule = entity.GetString(nameof(UserPreference.ExclusionRule)) ?? string.Empty,
                KeepUntilDate = entity.GetDateTimeOffset(nameof(UserPreference.KeepUntilDate)),
                IsActive = entity.GetBoolean(nameof(UserPreference.IsActive)) ?? true
            });
        }

        return preferences;
    }

    public async Task DeleteUserPreferenceAsync(string subscriptionId, string preferenceId, CancellationToken ct = default)
    {
        var client = await GetTableClientAsync(PreferenceTable, ct);
        try
        {
            await client.DeleteEntityAsync(subscriptionId, preferenceId, cancellationToken: ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Attempted to delete a preference that no longer exists: {PreferenceId}", preferenceId);
        }
    }

    private async Task<TableClient> GetTableClientAsync(string tableName, CancellationToken ct)
    {
        var client = _serviceClient.GetTableClient(tableName);
        await client.CreateIfNotExistsAsync(ct);
        return client;
    }

    /// <summary>Escapes single quotes for OData filter strings (basic injection guard).</summary>
    private static string Escape(string value) => value.Replace("'", "''");
}
