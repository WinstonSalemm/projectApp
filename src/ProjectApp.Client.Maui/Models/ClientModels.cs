namespace ProjectApp.Client.Maui.Models;

public enum ClientType
{
    Individual = 1,
    Company = 2,
    Retail = 3,
    Wholesale = 4,
    LargeWholesale = 5
}

public class ClientListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Inn { get; set; }
    public ClientType Type { get; set; }
    public string? OwnerUserName { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal TotalRevenue { get; set; }
    public bool HasOwner => !string.IsNullOrWhiteSpace(OwnerUserName);
}

public class ClientCreateDraft
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Inn { get; set; }
    public ClientType Type { get; set; } = ClientType.Individual;
}

public class ClientUpdateDraft
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Inn { get; set; }
    public ClientType? Type { get; set; }
}

