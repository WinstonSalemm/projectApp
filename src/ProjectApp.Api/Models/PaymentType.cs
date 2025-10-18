namespace ProjectApp.Api.Models;

public enum PaymentType
{
    CashWithReceipt = 0,
    CashNoReceipt = 1,
    CardWithReceipt = 2,
    Click = 3, // legacy, treated as grey/no receipt
    Site = 4,
    Return = 5,
    Reservation = 6,
    Payme = 7,
    ClickWithReceipt = 8,
    ClickNoReceipt = 9,
    Contract = 10,
    Debt = 11  // Отгрузка в долг (товар отгружен, оплата позже)
}
