namespace ProjectApp.Api.Models;

public enum ContractStatus
{
    Draft = 0,              // черновик
    Active = 1,             // активный (подписан)
    PartiallyPaid = 2,      // частично оплачен
    Paid = 3,               // полностью оплачен
    PartiallyDelivered = 4, // частично забрали товар
    Delivered = 5,          // товар забрали полностью
    Closed = 6,             // закрыт (оплачен и отгружен, все партии в IM-40)
    Cancelled = 7           // отменён
}
