namespace ProjectApp.Api.Models;

public class User
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "Manager"; // Admin | Manager
    public string PasswordHash { get; set; } = string.Empty;
    // If true, user can authenticate without password (intended for Manager role)
    public bool IsPasswordless { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
