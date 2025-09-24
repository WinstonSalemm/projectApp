using System;
using System.Collections.Generic;
namespace ProjectApp.Api.Models;

public class Return
{
    public int Id { get; set; }
    public int? RefSaleId { get; set; }
    public int? ClientId { get; set; }
    public decimal Sum { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Reason { get; set; }
    public List<ReturnItem> Items { get; set; } = new();
}
