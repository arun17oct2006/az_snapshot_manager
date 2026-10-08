namespace AzureSnapshotManager.Core.Models;

/// <summary>
/// Maps to ER entity "Scan Log" (Scan ID, Timestamp, Calculated Savings).
/// One row is written per advisory audit run (WBS 1.4.1 / 1.4.2), and
/// backs the "Trigger Advisory Audit" and "View Savings &amp; Cost Metrics"
/// use cases.
/// </summary>
public class ScanLog
{
    public string ScanId { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string SnapshotId { get; set; } = string.Empty;
    public decimal CalculatedSavings { get; set; }
    public string Reason { get; set; } = string.Empty;
}
