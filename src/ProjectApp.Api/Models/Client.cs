namespace ProjectApp.Api.Models;

public class Client
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Inn { get; set; }
    public ClientType Type { get; set; } = ClientType.Individual;
    public string? OwnerUserName { get; set; }
    public DateTime CreatedAt { get; set; }
}
