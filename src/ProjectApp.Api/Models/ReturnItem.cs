using System;
namespace ProjectApp.Api.Models
{
    public class ReturnItem
    {
        public int Id { get; set; }

        public int ReturnId { get; set; }
        public Return Return { get; set; } = default!;

        public int SaleItemId { get; set; }
        public SaleItem SaleItem { get; set; } = default!;

        public decimal Qty { get; set; }       // decimal(18,3)
        public decimal UnitPrice { get; set; } // decimal(18,2)
    }
}
