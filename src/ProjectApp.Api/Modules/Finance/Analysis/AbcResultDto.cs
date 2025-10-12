namespace ProjectApp.Api.Modules.Finance.Analysis;

public sealed class AbcItem
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Share { get; set; }
    public string Class { get; set; } = string.Empty;
}

public sealed class AbcResultDto
{
    public decimal TotalRevenue { get; set; }
    public IReadOnlyList<AbcItem> Items { get; set; } = Array.Empty<AbcItem>();
}
