namespace ProjectApp.Api.Modules.Finance.Analysis;

public sealed class XyzItem
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal MeanQty { get; set; }
    public decimal Cv { get; set; }
    public string Class { get; set; } = string.Empty;
}

public sealed class XyzResultDto
{
    public IReadOnlyList<XyzItem> Items { get; set; } = Array.Empty<XyzItem>();
}
