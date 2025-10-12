namespace ProjectApp.Api.Modules.Finance.Forecast;

public sealed class ForecastPoint
{
    public DateOnly Date { get; set; }
    public decimal Revenue { get; set; }
    public decimal? NetProfit { get; set; }
}

public sealed class ForecastDto
{
    public IReadOnlyList<ForecastPoint> Points { get; set; } = Array.Empty<ForecastPoint>();
    public string Model { get; set; } = "SMA30+LR(+Seasonal)";
}
