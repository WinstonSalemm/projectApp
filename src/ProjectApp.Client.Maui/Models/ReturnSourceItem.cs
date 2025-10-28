namespace ProjectApp.Client.Maui.Models;

/// <summary>
/// Единая модель для отображения операций, по которым можно сделать возврат
/// (продажи и договора)
/// </summary>
public class ReturnSourceItem
{
    public int Id { get; set; }
    public ReturnSourceType SourceType { get; set; }
    
    public string? ClientName { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    
    // Для продаж
    public PaymentType? PaymentType { get; set; }
    
    // Для договоров
    public string? ContractNumber { get; set; }
    
    // Для UI
    public string DisplayTitle => SourceType switch
    {
        ReturnSourceType.Sale => $"Продажа #{Id} - {Total:N0} сум",
        ReturnSourceType.Contract => $"Договор #{ContractNumber} - {Total:N0} сум",
        _ => $"#{Id}"
    };
    
    public string DisplaySubtitle => $"{ClientName ?? "Без клиента"} • {CreatedAt:dd.MM.yyyy HH:mm}";
    
    public string DisplayPaymentType => SourceType == ReturnSourceType.Sale && PaymentType.HasValue
        ? PaymentTypeToRu(PaymentType.Value)
        : "Договор";
    
    private static string PaymentTypeToRu(PaymentType pt) => pt switch
    {
        Models.PaymentType.CashWithReceipt => "Нал с чеком",
        Models.PaymentType.CashNoReceipt => "Нал без чека",
        Models.PaymentType.CardWithReceipt => "Карта с чеком",
        Models.PaymentType.ClickWithReceipt => "Click с чеком",
        Models.PaymentType.ClickNoReceipt => "Click без чека",
        Models.PaymentType.Contract => "Договор",
        _ => pt.ToString()
    };
}

public enum ReturnSourceType
{
    Sale,
    Contract
}
