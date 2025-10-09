namespace ProjectApp.Api.Models;

public enum ContractStatus
{
    Signed = 0,   // подписан
    Paid = 1,     // оплачен
    Closed = 2,   // закрыт полностью
    PartiallyClosed = 3, // закрыт частично
    Cancelled = 4        // отменён
}
