using System.Globalization;
using System.Text;
using AzureSnapshotManager.Core.Interfaces;
using AzureSnapshotManager.Core.Models;

namespace AzureSnapshotManager.Infrastructure.Export;

/// <summary>WBS 1.5.2 Advisory Itinerary CSV Export.</summary>
public class CsvExportService : IExportService
{
    public byte[] ExportToCsv(AdvisoryResult result, Func<Snapshot, string> deepLinkBuilder)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SnapshotId,Name,ResourceGroup,Location,SizeGB,TimeCreated,DailyCost,MonthlySavings,PortalLink");

        foreach (var snapshot in result.OrphanedSnapshots)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                Csv(snapshot.SnapshotId),
                Csv(snapshot.Name),
                Csv(snapshot.ResourceGroup),
                Csv(snapshot.Location),
                snapshot.SizeGb.ToString(CultureInfo.InvariantCulture),
                snapshot.TimeCreated.ToString("O"),
                snapshot.DailyCost.ToString("0.0000", CultureInfo.InvariantCulture),
                (snapshot.DailyCost * 30).ToString("0.00", CultureInfo.InvariantCulture),
                Csv(deepLinkBuilder(snapshot))
            }));
        }

        sb.AppendLine();
        sb.AppendLine($"Total,,,,,,,{result.TotalMonthlyWaste.ToString("0.00", CultureInfo.InvariantCulture)},");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Csv(string value)
        => value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
