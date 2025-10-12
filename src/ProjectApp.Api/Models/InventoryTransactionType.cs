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
    Reprice = 8
}
