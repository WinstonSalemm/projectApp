using System.Text;
using ProjectApp.Api.Modules.Finance.Dtos;

namespace ProjectApp.Api.Modules.Finance.Export;

public class FinanceExportService
{
    private readonly FinanceService _svc;
    public FinanceExportService(FinanceService svc) { _svc = svc; }

    public async Task<(byte[] Content, string FileName, string ContentType)> ExportSummaryAsync(DateTime? from, DateTime? to, string format, string? groupBy, CancellationToken ct)
    {
        var dto = await _svc.GetSummaryAsync(from, to, bucketBy: "day", groupBy: groupBy, ct);
        if (string.Equals(format, "excel", StringComparison.OrdinalIgnoreCase))
        {
            // CSV as lightweight Excel-friendly export
            var sb = new StringBuilder();
            sb.AppendLine("Metric,Value");
            sb.AppendLine($"Revenue,{dto.Revenue}");
            sb.AppendLine($"COGS,{dto.Cogs}");
            sb.AppendLine($"GrossProfit,{dto.GrossProfit}");
            sb.AppendLine($"NetProfit,{dto.NetProfit}");
            sb.AppendLine($"MarginPercent,{dto.MarginPercent}");
            sb.AppendLine($"Expenses,{dto.Expenses}");
            sb.AppendLine($"TaxesPaid,{dto.TaxesPaid}");
            sb.AppendLine($"SalesCount,{dto.SalesCount}");
            sb.AppendLine($"UniqueClients,{dto.UniqueClients}");
            if (dto.Groups is not null)
            {
                sb.AppendLine();
                sb.AppendLine("Group,Revenue,COGS,Gross");
                foreach (var g in dto.Groups)
                {
                    sb.AppendLine($"{g.Key},{g.Revenue},{g.Cogs},{g.Gross}");
                }
            }
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var name = $"finance-summary-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return (bytes, name, "text/csv");
        }
        // PDF placeholder: not implemented in lightweight mode
        var pdfBytes = Encoding.UTF8.GetBytes($"Finance Summary PDF placeholder\\nRevenue={dto.Revenue} Gross={dto.GrossProfit} Net={dto.NetProfit}");
        return (pdfBytes, $"finance-summary-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf", "application/pdf");
    }
}
