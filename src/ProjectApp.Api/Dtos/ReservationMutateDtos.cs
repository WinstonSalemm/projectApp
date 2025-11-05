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

public class ReservationPayDto
{
    public decimal Amount { get; set; }
    public ProjectApp.Api.Models.ReservationPaymentMethod Method { get; set; } = ProjectApp.Api.Models.ReservationPaymentMethod.Cash;
    public string? Note { get; set; }
}
