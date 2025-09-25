namespace ProjectApp.Api.Dtos;

public class ClientCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Inn { get; set; }
}

public class ClientUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Inn { get; set; }
}
