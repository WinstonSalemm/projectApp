using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace ProjectApp.Api.Models;

public class SupplyItem
{
    public int Id { get; set; }
    
    public int SupplyId { get; set; }
    public Supply Supply { get; set; } = null!;
    
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    
    [Required]
    [MaxLength(500)]
    public string Name { get; set; } = string.Empty; // Snapshot названия товара
    
    [MaxLength(200)]
    public string Sku { get; set; } = string.Empty; // Артикул товара
    
    public int Quantity { get; set; } // шт
    
    [Precision(18, 4)]
    public decimal PriceRub { get; set; } // за 1 шт в рублях
    
    [Precision(18, 4)]
    public decimal? PriceUsd { get; set; } // за 1 шт в USD (опционально)
    
    [Precision(18, 4)]
    public decimal? PriceUzs { get; set; } // за 1 шт в UZS (опционально)
    
    [Precision(18, 4)]
    public decimal Weight { get; set; } // Вес в кг
}
