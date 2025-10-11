namespace ProjectApp.Api.Dtos;

public class ReservationExtendDto
{
    public bool? Paid { get; set; }
    public int? Days { get; set; } // if null, use default by Paid flag
}

public class ReservationReleaseDto
{
    public string? Reason { get; set; }
}
