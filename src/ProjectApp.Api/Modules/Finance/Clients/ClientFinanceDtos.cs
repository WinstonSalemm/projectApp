namespace ProjectApp.Api.Modules.Finance.Clients;

public sealed class ClientFinanceRow
{
    public int ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Gross { get; set; }
    public int SalesCount { get; set; }
}

public sealed class ClientFinanceDto
{
    public IReadOnlyList<ClientFinanceRow> Clients { get; set; } = Array.Empty<ClientFinanceRow>();
}
