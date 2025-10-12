namespace ProjectApp.Api.Modules.Finance.CashFlow;

public sealed class CashFlowDto
{
    public decimal OperatingIn { get; set; }
    public decimal OperatingOut { get; set; }
    public decimal OCF { get; set; }
    public decimal InvestingIn { get; set; }
    public decimal InvestingOut { get; set; }
    public decimal ICF { get; set; }
    public decimal FinancingIn { get; set; }
    public decimal FinancingOut { get; set; }
    public decimal FCF { get; set; }
    public decimal NetCashFlow { get; set; }
}
