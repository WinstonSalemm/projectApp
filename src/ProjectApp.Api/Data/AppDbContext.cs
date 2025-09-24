using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Return> Returns => Set<Return>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<Debt> Debts => Set<Debt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.Sku).IsRequired().HasMaxLength(64);
            b.Property(p => p.Name).IsRequired().HasMaxLength(256);
            b.Property(p => p.Unit).IsRequired().HasMaxLength(16);
            b.Property(p => p.Price).HasColumnType("decimal(18,2)");
            b.HasIndex(p => p.Sku).IsUnique(false);
            b.HasData(
                new Product { Id = 1, Sku = "SKU-001", Name = "Coffee Beans 1kg", Unit = "kg", Price = 15.99m },
                new Product { Id = 2, Sku = "SKU-002", Name = "Tea Leaves 500g", Unit = "pkg", Price = 8.49m },
                new Product { Id = 3, Sku = "SKU-003", Name = "Sugar 1kg", Unit = "kg", Price = 2.29m },
                new Product { Id = 4, Sku = "SKU-004", Name = "Milk 1L", Unit = "ltr", Price = 1.19m },
                new Product { Id = 5, Sku = "SKU-005", Name = "Butter 200g", Unit = "pkg", Price = 3.79m },
                new Product { Id = 6, Sku = "SKU-006", Name = "Bread Loaf", Unit = "pc", Price = 1.99m },
                new Product { Id = 7, Sku = "SKU-007", Name = "Eggs (12)", Unit = "box", Price = 2.99m },
                new Product { Id = 8, Sku = "SKU-008", Name = "Olive Oil 500ml", Unit = "btl", Price = 6.49m },
                new Product { Id = 9, Sku = "SKU-009", Name = "Pasta 1kg", Unit = "kg", Price = 2.59m },
                new Product { Id = 10, Sku = "SKU-010", Name = "Tomato Sauce 300g", Unit = "jar", Price = 2.39m }
            );
        });

        modelBuilder.Entity<Client>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).IsRequired().HasMaxLength(200);
            b.Property(c => c.Phone).HasMaxLength(32);
            b.Property(c => c.Inn).HasMaxLength(32);
            b.HasData(
                new Client { Id = 1, Name = "Acme LLC", Phone = "+998 90 000 00 01", Inn = "123456789" },
                new Client { Id = 2, Name = "Globex Ltd", Phone = "+998 90 000 00 02", Inn = "223456789" },
                new Client { Id = 3, Name = "John Doe", Phone = "+998 90 000 00 03", Inn = null }
            );
        });

        modelBuilder.Entity<Sale>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.Total).HasColumnType("decimal(18,2)");
            b.Property(s => s.CreatedAt).IsRequired();
            b.HasMany<SaleItem>(s => s.Items)
             .WithOne()
             .HasForeignKey(i => i.SaleId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SaleItem>(b =>
        {
            b.HasKey(i => i.Id);
            b.Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
            b.Property(i => i.Qty).HasColumnType("decimal(18,3)");
        });

        modelBuilder.Entity<Return>(b =>
        {
            b.HasKey(r => r.Id);
            b.Property(r => r.Sum).HasColumnType("decimal(18,2)");
            b.Property(r => r.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<Stock>(b =>
        {
            b.HasKey(s => new { s.ProductId, s.Register });
            b.Property(s => s.Qty).HasColumnType("decimal(18,3)");
            var stocks = new List<Stock>();
            for (int pid = 1; pid <= 10; pid++)
            {
                stocks.Add(new Stock { ProductId = pid, Register = StockRegister.IM40, Qty = 100m });
                stocks.Add(new Stock { ProductId = pid, Register = StockRegister.ND40, Qty = 50m });
            }
            b.HasData(stocks);
        });

        modelBuilder.Entity<Debt>(b =>
        {
            b.HasKey(d => d.Id);
            b.Property(d => d.Amount).HasColumnType("decimal(18,2)");
            b.Property(d => d.DueDate).IsRequired();
        });
    }
}
