namespace ProjectApp.Client.Maui.Models;

public class CategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }

    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? "(No name)" : Name;
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public override string ToString() => string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName;
}

public class StoreOption
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }

    public override string ToString() => string.IsNullOrWhiteSpace(Name) ? Id : Name;
}

public enum SaleMethodKind
{
    CashWithReceipt,
    CashNoReceipt,
    CardWithReceipt,
    ClickWithReceipt,
    ClickNoReceipt,
    Site,
    Return,
    Reservation,
    Payme,
    Contract,
    CommissionClients
}

public class SaleMethodOption
{
    public SaleMethodKind Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public PaymentType? PaymentType { get; init; }
    public bool IsEnabled { get; init; } = true;
}
