using AzureSnapshotManager.Core.Models;

namespace AzureSnapshotManager.Core.Interfaces;

/// <summary>
/// WBS 1.2 Architecture &amp; Data Storage.
/// 1.2.2 Configure Azure Table Storage, 1.2.3 Enforce Customer Data Boundary
/// (partition key = subscription/tenant id, so one customer can never read
/// another customer's partition).
/// </summary>
public interface ISnapshotRepository
{
    Task SaveScanLogAsync(ScanLog log, string subscriptionId, CancellationToken ct = default);
    Task<List<ScanLog>> GetScanLogsAsync(string subscriptionId, CancellationToken ct = default);

    Task SaveUserPreferenceAsync(UserPreference preference, CancellationToken ct = default);
    Task<List<UserPreference>> GetUserPreferencesAsync(string subscriptionId, CancellationToken ct = default);
    Task DeleteUserPreferenceAsync(string subscriptionId, string preferenceId, CancellationToken ct = default);
}
