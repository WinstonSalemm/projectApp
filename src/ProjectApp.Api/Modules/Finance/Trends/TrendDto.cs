namespace ProjectApp.Api.Modules.Finance.Trends;

public sealed class TrendPoint { public DateOnly Period { get; set; } public decimal Value { get; set; } }
public sealed class TrendDto
{
    public string Metric { get; set; } = "revenue"; // revenue|gross|net
    public string Interval { get; set; } = "month"; // month|quarter|year
    public IReadOnlyList<TrendPoint> Series { get; set; } = Array.Empty<TrendPoint>();
    public decimal? YoYPercent { get; set; }
    public decimal? MoMPercent { get; set; }
}
