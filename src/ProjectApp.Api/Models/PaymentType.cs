namespace ProjectApp.Api.Models;

public enum PaymentType
{
    CashWithReceipt = 0,
    CashNoReceipt = 1,
    CardWithReceipt = 2,
    Click = 3, // legacy, treated as grey/no receipt
    Site = 4,
    Exchange = 5,
    Credit = 6,
    Payme = 7,
    ClickWithReceipt = 8,
    ClickNoReceipt = 9
}
