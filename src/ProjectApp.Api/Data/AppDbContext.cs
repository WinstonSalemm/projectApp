using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Models;
using ProjectApp.Api.Modules.Finance.Models;

namespace ProjectApp.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<CategoryRec> Categories => Set<CategoryRec>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<Return> Returns => Set<Return>();
    public DbSet<ReturnItem> ReturnItems => Set<ReturnItem>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<Debt> Debts => Set<Debt>();
    public DbSet<DebtItem> DebtItems => Set<DebtItem>();
    public DbSet<DebtPayment> DebtPayments => Set<DebtPayment>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ManagerStat> ManagerStats => Set<ManagerStat>();
    public DbSet<ManagerBonus> ManagerBonuses => Set<ManagerBonus>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<PromotionItem> PromotionItems => Set<PromotionItem>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ContractItem> ContractItems => Set<ContractItem>();
    public DbSet<ContractPayment> ContractPayments => Set<ContractPayment>();
    public DbSet<ContractDelivery> ContractDeliveries => Set<ContractDelivery>();
    public DbSet<ContractDeliveryBatch> ContractDeliveryBatches => Set<ContractDeliveryBatch>();
    public DbSet<SaleItemConsumption> SaleItemConsumptions => Set<SaleItemConsumption>();
    public DbSet<ReturnItemRestock> ReturnItemRestocks => Set<ReturnItemRestock>();
    public DbSet<SalePhoto> SalePhotos => Set<SalePhoto>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationItem> ReservationItems => Set<ReservationItem>();
    public DbSet<ReservationItemBatch> ReservationItemBatches => Set<ReservationItemBatch>();
    public DbSet<ReservationLog> ReservationLogs => Set<ReservationLog>();
    public DbSet<StockSnapshot> StockSnapshots => Set<StockSnapshot>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<TaxPayment> TaxPayments => Set<TaxPayment>();
    public DbSet<FinanceSnapshot> FinanceSnapshots => Set<FinanceSnapshot>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<InventoryConsumption> InventoryConsumptions => Set<InventoryConsumption>();
    public DbSet<ProductCostHistory> ProductCostHistories => Set<ProductCostHistory>();
    
    // Financial System
    public DbSet<Cashbox> Cashboxes => Set<Cashbox>();
    public DbSet<CashTransaction> CashTransactions => Set<CashTransaction>();
    public DbSet<OperatingExpense> OperatingExpenses => Set<OperatingExpense>();
    public DbSet<CashCollection> CashCollections => Set<CashCollection>();
    
    // Audit and Control
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    
    // Tax System
    public DbSet<TaxRecord> TaxRecords => Set<TaxRecord>();
    public DbSet<TaxSettings> TaxSettings => Set<TaxSettings>();
    
    // Commission (Partner Program)
    public DbSet<CommissionTransaction> CommissionTransactions => Set<CommissionTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Seed fire-safety products with categories
        var seedProducts = new List<Product>
        {
            new Product { Id = 1,  Sku = "OP-1",   Name = "ОП-1 (порошковый) 1 кг",            Unit = "шт", Price = 150000m, Category = "Огнетушители" },
            new Product { Id = 2,  Sku = "OP-2",   Name = "ОП-2 (порошковый) 2 кг",            Unit = "шт", Price = 200000m, Category = "Огнетушители" },
            new Product { Id = 3,  Sku = "OP-5",   Name = "ОП-5 (порошковый) 5 кг",            Unit = "шт", Price = 350000m, Category = "Огнетушители" },
            new Product { Id = 4,  Sku = "OU-2",   Name = "ОУ-2 (углекислотный) 2 кг",         Unit = "шт", Price = 400000m, Category = "Огнетушители" },
            new Product { Id = 5,  Sku = "OU-5",   Name = "ОУ-5 (углекислотный) 5 кг",         Unit = "шт", Price = 650000m, Category = "Огнетушители" },
            new Product { Id = 6,  Sku = "BR-OP2", Name = "Кронштейн настенный для ОП-2/ОУ-2", Unit = "шт", Price = 50000m,  Category = "Кронштейны"   },
            new Product { Id = 7,  Sku = "BR-OP5", Name = "Кронштейн настенный для ОП-5",      Unit = "шт", Price = 60000m,  Category = "Кронштейны"   },
            new Product { Id = 8,  Sku = "BR-UNI", Name = "Кронштейн универсальный металлический", Unit = "шт", Price = 70000m,  Category = "Кронштейны"   },
            new Product { Id = 9,  Sku = "ST-S",   Name = "Подставка под огнетушитель (малая)", Unit = "шт", Price = 80000m,  Category = "Подставки"     },
            new Product { Id = 10, Sku = "ST-D",   Name = "Подставка под огнетушители двойная", Unit = "шт", Price = 120000m, Category = "Подставки"     },
            new Product { Id = 11, Sku = "ST-FLR", Name = "Напольная стойка для огнетушителя",  Unit = "шт", Price = 180000m, Category = "Подставки"     },
            new Product { Id = 12, Sku = "CAB-1",  Name = "Шкаф для огнетушителя (металл)",     Unit = "шт", Price = 450000m, Category = "Шкафы"         }
        };

        modelBuilder.Entity<Product>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.Sku).IsRequired().HasMaxLength(64);
            b.Property(p => p.Name).IsRequired().HasMaxLength(256);
            b.Property(p => p.Unit).IsRequired().HasMaxLength(16);
            b.Property(p => p.Price).HasColumnType("decimal(18,2)");
            // b.Property(p => p.GtdCode).HasMaxLength(64); // Temporarily disabled - column doesn't exist in Railway
            b.HasIndex(p => p.Sku).IsUnique(false);
            b.HasData(seedProducts);
        });

        // Categories (optional directory of categories, in addition to Product.Category strings)
        modelBuilder.Entity<CategoryRec>(b =>
        {
            b.ToTable("Categories");
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).IsRequired().HasMaxLength(128);
            b.HasIndex(c => c.Name).IsUnique();
        });

        modelBuilder.Entity<Client>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).IsRequired().HasMaxLength(200);
            b.Property(c => c.Phone).HasMaxLength(32);
            b.Property(c => c.Inn).HasMaxLength(32);
            b.Property(c => c.OwnerUserName).HasMaxLength(64);
            b.Property(c => c.CreatedAt).IsRequired();
            b.HasData(
                new Client { Id = 1, Name = "Acme LLC", Phone = "+998 90 000 00 01", Inn = "123456789", Type = ClientType.Company, OwnerUserName = null, CreatedAt = new DateTime(2024,1,1,0,0,0,DateTimeKind.Utc) },
                new Client { Id = 2, Name = "Globex Ltd", Phone = "+998 90 000 00 02", Inn = "223456789", Type = ClientType.Company, OwnerUserName = null, CreatedAt = new DateTime(2024,1,1,0,0,0,DateTimeKind.Utc) },
                new Client { Id = 3, Name = "John Doe", Phone = "+998 90 000 00 03", Inn = null, Type = ClientType.Individual, OwnerUserName = null, CreatedAt = new DateTime(2024,1,1,0,0,0,DateTimeKind.Utc) }
            );
        });

        modelBuilder.Entity<Sale>(b =>
        {
            b.HasKey(s => s.Id);
            b.Property(s => s.Total).HasColumnType("decimal(18,2)");
            b.Property(s => s.CreatedAt).IsRequired();
            b.Property(s => s.ReservationNotes).HasColumnType("text");
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
            b.Property(i => i.Cost).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Return>(b =>
        {
            b.HasKey(r => r.Id);
            b.Property(r => r.Sum).HasColumnType("decimal(18,2)");
            b.Property(r => r.CreatedAt).IsRequired();
            b.Property(r => r.Reason).HasMaxLength(256);
            b.HasMany(r => r.Items)
             .WithOne(ri => ri.Return)
             .HasForeignKey(ri => ri.ReturnId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReturnItem>(b =>
        {
            b.HasKey(ri => ri.Id);
            b.Property(ri => ri.Qty).HasColumnType("decimal(18,3)");
            b.Property(ri => ri.UnitPrice).HasColumnType("decimal(18,2)");
            b.HasOne(ri => ri.SaleItem)
             .WithMany()
             .HasForeignKey(ri => ri.SaleItemId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Stock>(b =>
        {
            b.HasKey(s => new { s.ProductId, s.Register });
            b.Property(s => s.Qty).HasColumnType("decimal(18,3)");
            var stocks = new List<Stock>();
            foreach (var p in seedProducts)
            {
                stocks.Add(new Stock { ProductId = p.Id, Register = StockRegister.IM40, Qty = 100m });
                stocks.Add(new Stock { ProductId = p.Id, Register = StockRegister.ND40, Qty = 50m });
            }
            b.HasData(stocks);
        });

        // Seed initial batches to align with initial stocks (UnitCost=0 by default, can be edited later)
        var seedBatches = new List<Batch>();
        var seedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        int bid = 1;
        foreach (var p in seedProducts)
        {
            seedBatches.Add(new Batch { Id = bid++, ProductId = p.Id, Register = StockRegister.IM40, Qty = 100m, UnitCost = 0m, CreatedAt = seedDate, Note = "seed" });
            seedBatches.Add(new Batch { Id = bid++, ProductId = p.Id, Register = StockRegister.ND40, Qty = 50m,  UnitCost = 0m, CreatedAt = seedDate, Note = "seed" });
        }
        modelBuilder.Entity<Batch>().HasData(seedBatches);

        modelBuilder.Entity<Debt>(b =>
        {
            b.HasKey(d => d.Id);
            b.Property(d => d.Amount).HasColumnType("decimal(18,2)");
            b.Property(d => d.DueDate).IsRequired();
        });

        modelBuilder.Entity<DebtPayment>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            b.Property(p => p.PaidAt).IsRequired();
        });

        // Track from which batches each sale item consumed
        modelBuilder.Entity<SaleItemConsumption>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Qty).HasColumnType("decimal(18,3)");
            b.Property(x => x.RegisterAtSale).IsRequired();
            b.HasIndex(x => x.SaleItemId);
            b.HasIndex(x => x.BatchId);
        });

        // Track restock per return item back into batches
        modelBuilder.Entity<ReturnItemRestock>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Qty).HasColumnType("decimal(18,3)");
            b.HasIndex(x => x.ReturnItemId);
            b.HasIndex(x => new { x.SaleItemId, x.BatchId });
        });

        modelBuilder.Entity<Batch>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Qty).HasColumnType("decimal(18,3)");
            b.Property(x => x.UnitCost).HasColumnType("decimal(18,2)");
            b.Property(x => x.CreatedAt).IsRequired();
            b.Property(x => x.GtdCode).HasMaxLength(64);
            b.Property(x => x.ArchivedAt);
            b.HasIndex(x => new { x.ProductId, x.Register, x.CreatedAt, x.Id });
        });

        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(u => u.Id);
            b.Property(u => u.UserName).IsRequired().HasMaxLength(64);
            b.Property(u => u.DisplayName).IsRequired().HasMaxLength(128);
            b.Property(u => u.Role).IsRequired().HasMaxLength(32);
            b.Property(u => u.PasswordHash).IsRequired().HasMaxLength(512);
            b.Property(u => u.IsPasswordless).IsRequired();
            b.Property(u => u.IsActive).IsRequired();
            b.Property(u => u.CreatedAt).IsRequired();
            b.HasIndex(u => u.UserName).IsUnique();
        });

        modelBuilder.Entity<ManagerStat>(b =>
        {
            b.HasKey(m => m.UserName);
            b.Property(m => m.UserName).HasMaxLength(64);
            b.Property(m => m.SalesCount);
            b.Property(m => m.Turnover).HasColumnType("decimal(18,2)");
            b.Property(m => m.OwnedSalesCount);
            b.Property(m => m.OwnedTurnover).HasColumnType("decimal(18,2)");
        });

        // Contracts
        modelBuilder.Entity<Contract>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.OrgName).IsRequired().HasMaxLength(256);
            b.Property(c => c.Inn).HasMaxLength(32);
            b.Property(c => c.Phone).HasMaxLength(32);
            b.Property(c => c.Status).IsRequired();
            b.Property(c => c.CreatedAt).IsRequired();
            b.Property(c => c.Note).HasMaxLength(1024);
        });
        modelBuilder.Entity<ContractItem>(b =>
        {
            b.HasKey(i => i.Id);
            b.Property(i => i.Name).HasMaxLength(256);
            b.Property(i => i.Unit).HasMaxLength(16);
            b.Property(i => i.Qty).HasColumnType("decimal(18,3)");
            b.Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
            b.HasOne<Contract>()
             .WithMany(c => c.Items)
             .HasForeignKey(i => i.ContractId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SalePhoto>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.UserName).HasMaxLength(64);
            b.Property(x => x.Mime).HasMaxLength(64);
            b.Property(x => x.PathOrBlob).HasMaxLength(512);
            b.Property(x => x.CreatedAt).IsRequired();
            b.HasIndex(x => x.SaleId);
            b.HasIndex(x => x.UserName);
        });

        // Reservations (snapshot items with prices at reservation time)
        modelBuilder.Entity<Reservation>(b =>
        {
            b.HasKey(r => r.Id);
            b.Property(r => r.CreatedBy).HasMaxLength(64);
            b.Property(r => r.CreatedAt).IsRequired();
            b.Property(r => r.Paid).IsRequired();
            b.Property(r => r.ReservedUntil).IsRequired();
            b.Property(r => r.Status).IsRequired();
            b.Property(r => r.Note).HasMaxLength(1024);
            b.HasMany(r => r.Items)
             .WithOne()
             .HasForeignKey(i => i.ReservationId)
             .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(r => new { r.Status, r.ReservedUntil });
            b.HasIndex(r => r.CreatedBy);
            b.HasIndex(r => r.ClientId);
        });

        modelBuilder.Entity<ReservationItem>(b =>
        {
            b.HasKey(i => i.Id);
            b.Property(i => i.Qty).HasColumnType("decimal(18,3)");
            b.Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
            b.Property(i => i.Sku).HasMaxLength(64);
            b.Property(i => i.Name).HasMaxLength(256);
            b.HasIndex(i => new { i.ProductId, i.Register });
        });

        modelBuilder.Entity<ReservationLog>(b =>
        {
            b.HasKey(l => l.Id);
            b.Property(l => l.Action).HasMaxLength(32);
            b.Property(l => l.UserName).HasMaxLength(64);
            b.Property(l => l.CreatedAt).IsRequired();
            b.Property(l => l.Details).HasMaxLength(1024);
            b.HasIndex(l => l.ReservationId);
        });

        modelBuilder.Entity<StockSnapshot>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.NdQty).HasColumnType("decimal(18,3)");
            b.Property(x => x.ImQty).HasColumnType("decimal(18,3)");
            b.Property(x => x.TotalQty).HasColumnType("decimal(18,3)");
            b.Property(x => x.TotalValue).HasColumnType("decimal(18,2)");
            b.Property(x => x.CreatedAt).IsRequired();
            b.HasIndex(x => x.CreatedAt);
            b.HasIndex(x => x.ProductId);
        });

        modelBuilder.Entity<InventoryTransaction>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Qty).HasColumnType("decimal(18,3)");
            b.Property(x => x.UnitCost).HasColumnType("decimal(18,2)");
            b.Property(x => x.CreatedAt).IsRequired();
            b.HasIndex(x => new { x.ProductId, x.Register, x.CreatedAt });
            b.HasIndex(x => x.SaleId);
            b.HasIndex(x => x.ReturnId);
            b.HasIndex(x => x.ReservationId);
        });

        modelBuilder.Entity<InventoryConsumption>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Qty).HasColumnType("decimal(18,3)");
            b.Property(x => x.CreatedAt).IsRequired();
            b.HasIndex(x => new { x.ProductId, x.BatchId });
            b.HasIndex(x => x.SaleItemId);
            b.HasIndex(x => x.ReturnItemId);
        });

        modelBuilder.Entity<ProductCostHistory>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.UnitCost).HasColumnType("decimal(18,2)");
            b.Property(x => x.SnapshotAt).IsRequired();
            b.HasIndex(x => new { x.ProductId, x.SnapshotAt });
        });

        modelBuilder.Entity<Expense>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Category).HasMaxLength(64).IsRequired();
            b.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            b.Property(x => x.Date).IsRequired();
            b.Property(x => x.Note).HasMaxLength(512);
            b.HasIndex(x => x.Date);
            b.HasIndex(x => x.Category);
        });

        modelBuilder.Entity<TaxPayment>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Type).HasMaxLength(64);
            b.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            b.Property(x => x.PaidAt).IsRequired();
            b.Property(x => x.Note).HasMaxLength(256);
            b.HasIndex(x => x.PaidAt);
            b.HasIndex(x => x.Type);
        });

        modelBuilder.Entity<FinanceSnapshot>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Date).IsRequired();
            b.Property(x => x.Revenue).HasColumnType("decimal(18,2)");
            b.Property(x => x.Cogs).HasColumnType("decimal(18,2)");
            b.Property(x => x.GrossProfit).HasColumnType("decimal(18,2)");
            b.Property(x => x.Expenses).HasColumnType("decimal(18,2)");
            b.Property(x => x.TaxesPaid).HasColumnType("decimal(18,2)");
            b.Property(x => x.NetProfit).HasColumnType("decimal(18,2)");
            b.HasIndex(x => x.Date);
        });
    }
}
