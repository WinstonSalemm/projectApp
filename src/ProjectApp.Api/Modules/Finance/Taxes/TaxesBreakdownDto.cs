namespace ProjectApp.Api.Modules.Finance.Taxes;

public sealed class TaxesBreakdownDto
{
    public decimal VatAccrued { get; set; }
    public decimal ProfitTaxAccrued { get; set; }
    public decimal PayrollTaxAccrued { get; set; }
    public decimal SocialTaxAccrued { get; set; }
    public decimal TaxesPaid { get; set; }
    public decimal Delta => (VatAccrued + ProfitTaxAccrued + PayrollTaxAccrued + SocialTaxAccrued) - TaxesPaid;
}
