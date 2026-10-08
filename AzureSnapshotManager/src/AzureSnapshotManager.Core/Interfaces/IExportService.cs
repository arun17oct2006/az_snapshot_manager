using AzureSnapshotManager.Core.Models;

namespace AzureSnapshotManager.Core.Interfaces;

/// <summary>
/// WBS 1.5.2 Advisory Itinerary CSV Export.
/// Backs use case "Export Reports &amp; Portal Deep Links".
/// </summary>
public interface IExportService
{
    /// <summary>Renders an advisory result to CSV bytes, including a Portal deep-link column.</summary>
    byte[] ExportToCsv(AdvisoryResult result, Func<Snapshot, string> deepLinkBuilder);
}
