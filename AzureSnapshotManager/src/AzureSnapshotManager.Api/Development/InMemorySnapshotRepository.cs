using System.Collections.Concurrent;
using AzureSnapshotManager.Core.Interfaces;
using AzureSnapshotManager.Core.Models;

namespace AzureSnapshotManager.Api.Development;

/// <summary>
/// LOCAL DEVELOPMENT ONLY. Keeps scan logs and retention preferences in
/// memory instead of Azure Table Storage, so you can exercise the full
/// discovery -> pricing -> advisory pipeline against your REAL Azure
/// subscription (via az login) without first provisioning a storage
/// account. Data is lost on every restart — that's expected.
///
/// Wired up in Program.cs ONLY when builder.Environment.IsDevelopment() is
/// true. Every other environment uses the real TableStorageRepository.
/// </summary>
public class InMemorySnapshotRepository : ISnapshotRepository
{
    // Keyed by subscriptionId, mirroring the PartitionKey boundary the real
    // TableStorageRepository enforces — so behavior stays consistent
    // between local testing and production.
    private readonly ConcurrentDictionary<string, List<ScanLog>> _scanLogs = new();
    private readonly ConcurrentDictionary<string, List<UserPreference>> _preferences = new();

    public Task SaveScanLogAsync(ScanLog log, string subscriptionId, CancellationToken ct = default)
    {
        var logs = _scanLogs.GetOrAdd(subscriptionId, _ => new List<ScanLog>());
        lock (logs) { logs.Add(log); }
        return Task.CompletedTask;
    }

    public Task<List<ScanLog>> GetScanLogsAsync(string subscriptionId, CancellationToken ct = default)
    {
        var logs = _scanLogs.GetOrAdd(subscriptionId, _ => new List<ScanLog>());
        lock (logs) { return Task.FromResult(logs.ToList()); }
    }

    public Task SaveUserPreferenceAsync(UserPreference preference, CancellationToken ct = default)
    {
        var prefs = _preferences.GetOrAdd(preference.SubscriptionId, _ => new List<UserPreference>());
        lock (prefs)
        {
            prefs.RemoveAll(p => p.PreferenceId == preference.PreferenceId);
            prefs.Add(preference);
        }
        return Task.CompletedTask;
    }

    public Task<List<UserPreference>> GetUserPreferencesAsync(string subscriptionId, CancellationToken ct = default)
    {
        var prefs = _preferences.GetOrAdd(subscriptionId, _ => new List<UserPreference>());
        lock (prefs) { return Task.FromResult(prefs.ToList()); }
    }

    public Task DeleteUserPreferenceAsync(string subscriptionId, string preferenceId, CancellationToken ct = default)
    {
        if (_preferences.TryGetValue(subscriptionId, out var prefs))
        {
            lock (prefs) { prefs.RemoveAll(p => p.PreferenceId == preferenceId); }
        }
        return Task.CompletedTask;
    }
}
