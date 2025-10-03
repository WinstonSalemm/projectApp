using System.Collections.ObjectModel;

namespace ProjectApp.Api.Models;

public class Contract
{
    public int Id { get; set; }
    public string OrgName { get; set; } = string.Empty;
    public string? Inn { get; set; }
    public string? Phone { get; set; }
    public ContractStatus Status { get; set; } = ContractStatus.Signed;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }

    public List<ContractItem> Items { get; set; } = new();
}
