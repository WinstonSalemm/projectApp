using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;

namespace ProjectApp.Api.Tests;

public class SqliteDbFixture
{
    public AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AppDbContext(options);
        db.Database.Migrate();
        return db;
    }
}
