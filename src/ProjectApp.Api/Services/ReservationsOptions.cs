namespace ProjectApp.Api.Services;

public class ReservationsOptions
{
    public int UnpaidDays { get; set; } = 3;
    public int PaidDays { get; set; } = 10;
    public int PaidReminderDay { get; set; } = 7;
    public PhotoOptions Photo { get; set; } = new();

    public class PhotoOptions
    {
        public int MaxBytes { get; set; } = 2 * 1024 * 1024;
        public int MaxLongSide { get; set; } = 1080;
        public int JpegQuality { get; set; } = 75;
    }
}
