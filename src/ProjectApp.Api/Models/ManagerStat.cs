namespace ProjectApp.Api.Models;

public class ManagerStat
{
    // Primary key: manager user name (same as ClaimTypes.Name / Users.UserName)
    public string UserName { get; set; } = string.Empty;
    public int SalesCount { get; set; }
    public decimal Turnover { get; set; }
}
