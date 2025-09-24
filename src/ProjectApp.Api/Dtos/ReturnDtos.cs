using System.Collections.Generic;
namespace ProjectApp.Api.Dtos;

public class ReturnCreateDto
{
    public int RefSaleId { get; set; }
    public int? ClientId { get; set; }
    public string? Reason { get; set; }
    // If null or empty -> full return
    public List<ReturnItemCreateDto>? Items { get; set; }
}
