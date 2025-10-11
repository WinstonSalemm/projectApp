namespace ProjectApp.Api.Models;

public class SalePhoto
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Mime { get; set; } = "image/jpeg";
    public long Size { get; set; }
    public DateTime CreatedAt { get; set; }
    // File path on disk (server side)
    public string PathOrBlob { get; set; } = string.Empty;
}
