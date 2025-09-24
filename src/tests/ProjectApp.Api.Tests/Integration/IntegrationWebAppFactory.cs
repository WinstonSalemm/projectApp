using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;

namespace ProjectApp.Api.Tests.Integration;

public class IntegrationWebAppFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("environment", "Development");
        builder.ConfigureServices(services =>
        {
            // Replace AppDbContext with in-memory Sqlite
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // Build provider and apply migrations once on factory creation
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        });
    }

    public async Task ResetDatabaseAsync()
    {
        // Reset data to initial seeded state
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Remove mutable data
        await db.SaleItems.ExecuteDeleteAsync();
        await db.Sales.ExecuteDeleteAsync();
        await db.Returns.ExecuteDeleteAsync();
        await db.Debts.ExecuteDeleteAsync();
        await db.Stocks.ExecuteDeleteAsync();

        // Re-seed stocks according to OnModelCreating
        for (int pid = 1; pid <= 10; pid++)
        {
            db.Stocks.Add(new Stock { ProductId = pid, Register = StockRegister.IM40, Qty = 100m });
            db.Stocks.Add(new Stock { ProductId = pid, Register = StockRegister.ND40, Qty = 50m });
        }
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
