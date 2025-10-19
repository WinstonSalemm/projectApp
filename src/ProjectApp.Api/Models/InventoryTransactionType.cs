namespace ProjectApp.Api.Models;

public enum InventoryTransactionType
{
    Purchase = 0,
    Sale = 1,
    ReturnIn = 2,
    ReturnOut = 3,
    MoveNdToIm = 4,
    Adjust = 5,
    Reserve = 6,
    Release = 7,
    Reprice = 8,
    Reservation = 9,              // Бронирование товара
    ReservationCancelled = 10,    // Отмена бронирования
    ContractDelivery = 11,        // Отгрузка по договору
    Defective = 12,               // Списание брака
    DefectiveCancelled = 13,      // Отмена списания брака (возврат)
    Refill = 14                   // Перезарядка (огнетушители)
}
