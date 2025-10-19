using Microsoft.Data.Sqlite;

var dbPath = "../src/ProjectApp.Api/projectapp.db";

if (!File.Exists(dbPath))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"ERROR: Database {dbPath} not found!");
    Console.ResetColor();
    return 1;
}

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine($"Applying Defectives & Refills migration to {dbPath}...");
Console.ResetColor();

using var connection = new SqliteConnection($"Data Source={dbPath}");
connection.Open();

using var transaction = connection.BeginTransaction();

try
{
    // 1. Create DefectiveItems table
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS DefectiveItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductId INTEGER NOT NULL,
                ProductName TEXT NOT NULL,
                Sku TEXT,
                Quantity INTEGER NOT NULL,
                Warehouse INTEGER NOT NULL DEFAULT 0,
                Reason TEXT,
                Status INTEGER NOT NULL DEFAULT 0,
                CreatedBy TEXT NOT NULL,
                CreatedAt DATETIME NOT NULL DEFAULT (datetime('now')),
                CancelledBy TEXT,
                CancelledAt DATETIME,
                CancellationReason TEXT,
                FOREIGN KEY (ProductId) REFERENCES Products(Id)
            )";
        cmd.ExecuteNonQuery();
        Console.WriteLine("✓ DefectiveItems table created");
    }

    // 2. Create indexes for DefectiveItems
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_DefectiveItems_ProductId ON DefectiveItems(ProductId)";
        cmd.ExecuteNonQuery();
    }
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_DefectiveItems_CreatedAt ON DefectiveItems(CreatedAt DESC)";
        cmd.ExecuteNonQuery();
    }
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_DefectiveItems_Status ON DefectiveItems(Status)";
        cmd.ExecuteNonQuery();
    }
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_DefectiveItems_Warehouse ON DefectiveItems(Warehouse)";
        cmd.ExecuteNonQuery();
    }
    Console.WriteLine("✓ DefectiveItems indexes created");

    // 3. Create RefillOperations table
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS RefillOperations (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductId INTEGER NOT NULL,
                ProductName TEXT NOT NULL,
                Sku TEXT,
                Quantity INTEGER NOT NULL,
                Warehouse INTEGER NOT NULL DEFAULT 0,
                CostPerUnit DECIMAL(18,2) NOT NULL DEFAULT 0,
                TotalCost DECIMAL(18,2) NOT NULL DEFAULT 0,
                Notes TEXT,
                Status INTEGER NOT NULL DEFAULT 0,
                CreatedBy TEXT NOT NULL,
                CreatedAt DATETIME NOT NULL DEFAULT (datetime('now')),
                CancelledBy TEXT,
                CancelledAt DATETIME,
                CancellationReason TEXT,
                FOREIGN KEY (ProductId) REFERENCES Products(Id)
            )";
        cmd.ExecuteNonQuery();
        Console.WriteLine("✓ RefillOperations table created");
    }

    // 4. Create indexes for RefillOperations
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_RefillOperations_ProductId ON RefillOperations(ProductId)";
        cmd.ExecuteNonQuery();
    }
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_RefillOperations_CreatedAt ON RefillOperations(CreatedAt DESC)";
        cmd.ExecuteNonQuery();
    }
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_RefillOperations_Status ON RefillOperations(Status)";
        cmd.ExecuteNonQuery();
    }
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "CREATE INDEX IF NOT EXISTS IX_RefillOperations_Warehouse ON RefillOperations(Warehouse)";
        cmd.ExecuteNonQuery();
    }
    Console.WriteLine("✓ RefillOperations indexes created");

    transaction.Commit();
    
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("SUCCESS! Migration applied.");
    Console.ResetColor();
    Console.WriteLine();
    
    // Verify tables created
    using (var cmd = connection.CreateCommand())
    {
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('DefectiveItems', 'RefillOperations')";
        using var reader = cmd.ExecuteReader();
        
        Console.WriteLine("Tables created:");
        while (reader.Read())
        {
            Console.WriteLine($"  - {reader.GetString(0)}");
        }
    }
    
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("Done! You can now run the application.");
    Console.ResetColor();
    
    return 0;
}
catch (Exception ex)
{
    transaction.Rollback();
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"ERROR: {ex.Message}");
    Console.ResetColor();
    return 1;
}
