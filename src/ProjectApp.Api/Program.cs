using Serilog;
using System.Text.Json.Serialization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ProjectApp.Api.Data;
using ProjectApp.Api.Repositories;
using ProjectApp.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using ProjectApp.Api.Auth;
using ProjectApp.Api.Integrations.Telegram;
using ProjectApp.Api.Middleware;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, services, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application","ProjectApp.Api")
    .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter(renderMessage: true))
);

// Allow overriding configuration via environment variables with prefix PROJECTAPP__
// Example: PROJECTAPP__Cors__Origins__0=http://localhost:5028
builder.Configuration.AddEnvironmentVariables(prefix: "PROJECTAPP__");

// CORS - Named policy based on environment
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCors", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Dev: allow all to keep current local workflow simple
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            // Prod/Non-Dev: restrict to configured origins, no credentials
            var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
            if (origins.Length > 0)
            {
                policy.WithOrigins(origins)
                      .AllowAnyMethod()
                      .AllowAnyHeader(); // Credentials are NOT allowed by default
            }
            else
            {
                // No origins configured => block cross-origin by default
                policy.SetIsOriginAllowed(_ => false)
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            }
        }
    });
});

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        if (ctx.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var cidObj) && cidObj is string cid)
        {
            ctx.ProblemDetails.Extensions["correlationId"] = cid;
        }
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // ApiKey security definition
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ProjectApp API", Version = "v1" });
    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API Key required for mutation endpoints. Provide your API key.",
        In = ParameterLocation.Header,
        Name = "X-API-KEY",
        Type = SecuritySchemeType.ApiKey,
        Scheme = ApiKeyAuthenticationOptions.DefaultScheme
    });
    // Bearer security definition (JWT)
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    // Apply requirement only to [Authorize] endpoints via operation filter
    c.OperationFilter<ProjectApp.Api.Swagger.AuthorizeCheckOperationFilter>();
});

// EF Core - Auto provider: MySQL (Pomelo) if MySQL-like connection string; else SQLite (local dev)
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection");

    static bool LooksLikeMySql(string? cs)
        => !string.IsNullOrWhiteSpace(cs) &&
           (cs.Contains("Server=", StringComparison.OrdinalIgnoreCase)
            || cs.Contains("Host=", StringComparison.OrdinalIgnoreCase));

    if (LooksLikeMySql(conn))
    {
        var serverVersion = Microsoft.EntityFrameworkCore.ServerVersion.AutoDetect(conn!);
        options.UseMySql(conn!, serverVersion);
    }
    else
    {
        // SQLite fallback (ContentRoot anchored file if relative)
        string finalConn;
        if (string.IsNullOrWhiteSpace(conn))
        {
            // In non-Dev cloud environments (like Railway), ContentRoot may be read-only.
            // Use OS temp directory instead to ensure write access.
            string basePath;
            if (builder.Environment.IsDevelopment())
            {
                basePath = builder.Environment.ContentRootPath;
            }
            else
            {
                basePath = Path.GetTempPath(); // e.g., /tmp on Linux
            }
            string dbPath = Path.Combine(basePath, "projectapp.db");
            try
            {
                var dir = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            }
            catch { }
            finalConn = $"Data Source={dbPath}";
        }
        else
        {
            var parts = conn.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string? dataSourcePart = parts.FirstOrDefault(p => p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) || p.StartsWith("DataSource=", StringComparison.OrdinalIgnoreCase));
            if (dataSourcePart is not null)
            {
                var kv = dataSourcePart.Split('=');
                var pathVal = kv.Length > 1 ? kv[1] : string.Empty;
                if (!string.IsNullOrWhiteSpace(pathVal) && !Path.IsPathRooted(pathVal))
                {
                    string basePath;
                    if (builder.Environment.IsDevelopment())
                    {
                        basePath = builder.Environment.ContentRootPath;
                    }
                    else
                    {
                        basePath = Path.GetTempPath();
                    }
                    string dbPath = Path.Combine(basePath, pathVal);
                    try
                    {
                        var dir = Path.GetDirectoryName(dbPath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    }
                    catch { }
                    finalConn = $"Data Source={dbPath}";
                }
                else
                {
                    finalConn = conn;
                }
            }
            else
            {
                string basePath = builder.Environment.IsDevelopment() ? builder.Environment.ContentRootPath : Path.GetTempPath();
                string dbPath = Path.Combine(basePath, "projectapp.db");
                try
                {
                    var dir = Path.GetDirectoryName(dbPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                }
                catch { }
                finalConn = $"Data Source={dbPath}";
            }
        }

        options.UseSqlite(finalConn);
    }
});

// DI
builder.Services.AddScoped<IProductRepository, EfProductRepository>();
builder.Services.AddScoped<ISaleRepository, EfSaleRepository>();
builder.Services.AddScoped<ISaleCalculator, SaleCalculator>();
builder.Services.AddSingleton<ProjectApp.Api.Services.IPasswordHasher, ProjectApp.Api.Services.PasswordHasher>();

// Telegram integration
builder.Services.Configure<TelegramSettings>(builder.Configuration.GetSection("Telegram"));
builder.Services.AddHttpClient("telegram");
builder.Services.AddSingleton<ITelegramService, TelegramService>();
builder.Services.AddSingleton<ISalesNotifier, SalesNotifier>();
builder.Services.AddHostedService<DailySummaryHostedService>();
builder.Services.AddSingleton<ProjectApp.Api.Integrations.Telegram.IReturnsNotifier, ProjectApp.Api.Integrations.Telegram.ReturnsNotifier>();
builder.Services.AddSingleton<ProjectApp.Api.Integrations.Telegram.IDebtsNotifier, ProjectApp.Api.Integrations.Telegram.DebtsNotifier>();

// Authentication & Authorization
// JWT settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
// Provide safe defaults if not configured (prevents 500 on login)
builder.Services.PostConfigure<JwtSettings>(opts =>
{
    if (string.IsNullOrWhiteSpace(opts.Secret))
        opts.Secret = "dev-secret-please-override-140606tl-0123456789abcdef0123456789abcdef"; // override via config
    if (string.IsNullOrWhiteSpace(opts.Issuer))
        opts.Issuer = "ProjectApp";
    if (string.IsNullOrWhiteSpace(opts.Audience))
        opts.Audience = "ProjectApp.Clients";
    if (opts.AccessTokenLifetimeMinutes <= 0)
        opts.AccessTokenLifetimeMinutes = 720;
});
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

// Authentication: support both JWT and ApiKey
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
        // Fallbacks in case configuration is missing
        if (string.IsNullOrWhiteSpace(jwt.Secret))
        {
            jwt.Secret = "dev-secret-please-override-140606tl-0123456789abcdef0123456789abcdef";
            jwt.Issuer = string.IsNullOrWhiteSpace(jwt.Issuer) ? "ProjectApp" : jwt.Issuer;
            jwt.Audience = string.IsNullOrWhiteSpace(jwt.Audience) ? "ProjectApp.Clients" : jwt.Audience;
            if (jwt.AccessTokenLifetimeMinutes <= 0) jwt.AccessTokenLifetimeMinutes = 720;
        }
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    })
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.DefaultScheme, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireApiKey", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddAuthenticationSchemes(ApiKeyAuthenticationOptions.DefaultScheme, JwtBearerDefaults.AuthenticationScheme);
    });

    options.AddPolicy("ManagerOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Manager", "Admin");
    });

    options.AddPolicy("AdminOnly", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole("Admin");
    });
});

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("db", tags: new[] { "ready" });

var app = builder.Build();

// ---------- DB init (provider-aware) ----------
await using (var scope = app.Services.CreateAsyncScope())
{
    // 0) If MySQL is configured, ensure the target database (schema) exists
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var connForDetect = configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(connForDetect) &&
        (connForDetect.Contains("Server=", StringComparison.OrdinalIgnoreCase) || connForDetect.Contains("Host=", StringComparison.OrdinalIgnoreCase)))
    {
        try
        {
            var csb = new MySqlConnectionStringBuilder(connForDetect);
            var targetDb = csb.Database;
            if (!string.IsNullOrWhiteSpace(targetDb))
            {
                csb.Database = string.Empty; // connect to server without schema
                await using var adminConn = new MySqlConnection(csb.ConnectionString);
                await adminConn.OpenAsync();
                await using var cmd = adminConn.CreateCommand();
                cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{targetDb}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                await cmd.ExecuteNonQueryAsync();
            }
        }
        catch
        {
            // ignore bootstrap errors; context init may still succeed if DB exists
        }
    }

    // 1) Now use EF Core context to create schema/migrations
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var provider = db.Database.ProviderName ?? string.Empty;
    if (provider.Contains("MySql", StringComparison.OrdinalIgnoreCase))
    {
        // Generate schema from model for MySQL
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        var pending = await db.Database.GetPendingMigrationsAsync();
        if (pending.Any())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();
    }

    // 1.1) Minimal schema patchers (idempotent)
    try
    {
        var provider2 = db.Database.ProviderName ?? string.Empty;
        if (provider2.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            var costExists = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'SaleItems' AND COLUMN_NAME = 'Cost'";
                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar != null && scalar != DBNull.Value)
                {
                    var cnt = Convert.ToInt64(scalar);
                    costExists = cnt > 0;
                }
            }
            if (!costExists)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE `SaleItems` ADD COLUMN `Cost` DECIMAL(18,2) NOT NULL DEFAULT 0;");
            }

            // Ensure Batches.Code exists
            var codeExists = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Batches' AND COLUMN_NAME = 'Code'";
                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar != null && scalar != DBNull.Value)
                {
                    var cnt = Convert.ToInt64(scalar);
                    codeExists = cnt > 0;
                }
            }
            if (!codeExists)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE `Batches` ADD COLUMN `Code` VARCHAR(128) NULL;");
            }
        }
        else if (provider2.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            // Check PRAGMA for column presence
            var hasCost = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('SaleItems');";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(1);
                    if (string.Equals(name, "Cost", StringComparison.OrdinalIgnoreCase)) { hasCost = true; break; }
                }
            }
            if (!hasCost)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE SaleItems ADD COLUMN Cost DECIMAL(18,2) NOT NULL DEFAULT 0;");
            }

            // Ensure Batches.Code exists
            var hasCode = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('Batches');";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(1);
                    if (string.Equals(name, "Code", StringComparison.OrdinalIgnoreCase)) { hasCode = true; break; }
                }
            }
            if (!hasCode)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Batches ADD COLUMN Code TEXT NULL;");
            }
        }
    }
    catch
    {
        // ignore patch errors; app remains functional and sales flow will still work
    }

    // 1.2) Ensure Users table exists (provider-specific) before seeding users
    try
    {
        var provider3 = db.Database.ProviderName ?? string.Empty;
        if (provider3.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            var exists = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Users'";
                var scalar = await cmd.ExecuteScalarAsync();
                exists = scalar != null && scalar != DBNull.Value && Convert.ToInt64(scalar) > 0;
            }
            if (!exists)
            {
                var sql = @"CREATE TABLE `Users` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `UserName` VARCHAR(64) NOT NULL,
  `DisplayName` VARCHAR(128) NOT NULL,
  `Role` VARCHAR(32) NOT NULL,
  `PasswordHash` VARCHAR(512) NOT NULL,
  `IsPasswordless` TINYINT(1) NOT NULL,
  `IsActive` TINYINT(1) NOT NULL,
  `CreatedAt` DATETIME(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE INDEX `IX_Users_UserName` (`UserName` ASC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
                await db.Database.ExecuteSqlRawAsync(sql);
            }
            else
            {
                // Patch: add IsPasswordless column if missing
                var hasCol = false;
                using (var conn = db.Database.GetDbConnection())
                {
                    await conn.OpenAsync();
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Users' AND COLUMN_NAME = 'IsPasswordless'";
                    var scalar = await cmd.ExecuteScalarAsync();
                    hasCol = scalar != null && scalar != DBNull.Value && Convert.ToInt64(scalar) > 0;
                }
                if (!hasCol)
                {
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE `Users` ADD COLUMN `IsPasswordless` TINYINT(1) NOT NULL DEFAULT 0;");
                }
            }
        }
        else if (provider3.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var exists = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Users'";
                var scalar = await cmd.ExecuteScalarAsync();
                exists = scalar != null && scalar != DBNull.Value && Convert.ToInt64(scalar) > 0;
            }
            if (!exists)
            {
                var sql = @"CREATE TABLE Users (
  Id INTEGER NOT NULL CONSTRAINT PK_Users PRIMARY KEY AUTOINCREMENT,
  UserName TEXT NOT NULL,
  DisplayName TEXT NOT NULL,
  Role TEXT NOT NULL,
  PasswordHash TEXT NOT NULL,
  IsPasswordless INTEGER NOT NULL,
  IsActive INTEGER NOT NULL,
  CreatedAt TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_Users_UserName ON Users(UserName);";
                await db.Database.ExecuteSqlRawAsync(sql);
            }
            else
            {
                // Patch: add IsPasswordless column if missing
                var hasCol = false;
                using (var conn = db.Database.GetDbConnection())
                {
                    await conn.OpenAsync();
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = "PRAGMA table_info('Users');";
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var name = reader.GetString(1);
                        if (string.Equals(name, "IsPasswordless", StringComparison.OrdinalIgnoreCase)) { hasCol = true; break; }
                    }
                }
                if (!hasCol)
                {
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN IsPasswordless INTEGER NOT NULL DEFAULT 0;");
                }
            }
        }
    }
    catch { }

    // 1.2.1) Ensure ManagerStats table exists (provider-specific)
    try
    {
        var provider4 = db.Database.ProviderName ?? string.Empty;
        if (provider4.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            var exists = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ManagerStats'";
                var scalar = await cmd.ExecuteScalarAsync();
                exists = scalar != null && scalar != DBNull.Value && Convert.ToInt64(scalar) > 0;
            }
            if (!exists)
            {
                var sql = @"CREATE TABLE `ManagerStats` (
  `UserName` VARCHAR(64) NOT NULL,
  `SalesCount` INT NOT NULL,
  `Turnover` DECIMAL(18,2) NOT NULL,
  PRIMARY KEY (`UserName`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;";
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }
        else if (provider4.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var exists = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ManagerStats'";
                var scalar = await cmd.ExecuteScalarAsync();
                exists = scalar != null && scalar != DBNull.Value && Convert.ToInt64(scalar) > 0;
            }
            if (!exists)
            {
                var sql = @"CREATE TABLE ManagerStats (
  UserName TEXT NOT NULL CONSTRAINT PK_ManagerStats PRIMARY KEY,
  SalesCount INTEGER NOT NULL,
  Turnover DECIMAL(18,2) NOT NULL
);";
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }
    }
    catch { }

    // 1.3) Seed default admin user if none exist
    try
    {
        if (!await db.Users.AnyAsync())
        {
            var hasher = scope.ServiceProvider.GetRequiredService<ProjectApp.Api.Services.IPasswordHasher>();
            var admin = new ProjectApp.Api.Models.User
            {
                UserName = "admin",
                DisplayName = "Администратор",
                Role = "Admin",
                IsPasswordless = false,
                PasswordHash = hasher.Hash("140606tl"),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }
        // Ensure 'shop' manager (Магазин) exists
        try
        {
            var hasher = scope.ServiceProvider.GetRequiredService<ProjectApp.Api.Services.IPasswordHasher>();
            if (!await db.Users.AnyAsync(u => u.UserName == "shop"))
            {
                var shop = new ProjectApp.Api.Models.User
                {
                    UserName = "shop",
                    DisplayName = "Магазин",
                    Role = "Manager",
                    IsPasswordless = true,
                    PasswordHash = hasher.Hash(string.Empty),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                db.Users.Add(shop);
                await db.SaveChangesAsync();
            }

            // Ensure 6 named managers exist (passwordless)
            var managerSeeds = new (string UserName, string DisplayName)[]
            {
                ("liliya", "Лилия"),
                ("timur", "Тимур"),
                ("valeriy", "Валерий"),
                ("albert", "Альберт"),
                ("rasim", "Расим"),
                ("alisher", "Алишер"),
            };
            foreach (var m in managerSeeds)
            {
                if (!await db.Users.AnyAsync(u => u.UserName == m.UserName))
                {
                    var u = new ProjectApp.Api.Models.User
                    {
                        UserName = m.UserName,
                        DisplayName = m.DisplayName,
                        Role = "Manager",
                        IsPasswordless = true,
                        PasswordHash = hasher.Hash(string.Empty),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    db.Users.Add(u);
                }
            }
            await db.SaveChangesAsync();
        }
        catch { }
    }
    catch { }

    var seedEnabled = app.Configuration.GetValue("Seed:Enabled", true);
    if (seedEnabled)
    {
        // If there are no fire-safety products yet, seed them idempotently by SKU
        var hasFire = await db.Products.AnyAsync(p => p.Category == "Огнетушители");
        if (!hasFire)
        {
            var toAdd = new List<ProjectApp.Api.Models.Product>
            {
                new() { Sku = "OP-1",   Name = "ОП-1 (порошковый) 1 кг",            Unit = "шт", Price = 150000m, Category = "Огнетушители" },
                new() { Sku = "OP-2",   Name = "ОП-2 (порошковый) 2 кг",            Unit = "шт", Price = 200000m, Category = "Огнетушители" },
                new() { Sku = "OP-5",   Name = "ОП-5 (порошковый) 5 кг",            Unit = "шт", Price = 350000m, Category = "Огнетушители" },
                new() { Sku = "OU-2",   Name = "ОУ-2 (углекислотный) 2 кг",         Unit = "шт", Price = 400000m, Category = "Огнетушители" },
                new() { Sku = "OU-5",   Name = "ОУ-5 (углекислотный) 5 кг",         Unit = "шт", Price = 650000m, Category = "Огнетушители" },
                new() { Sku = "BR-OP2", Name = "Кронштейн настенный для ОП-2/ОУ-2", Unit = "шт", Price = 50000m,  Category = "Кронштейны"   },
                new() { Sku = "BR-OP5", Name = "Кронштейн настенный для ОП-5",      Unit = "шт", Price = 60000m,  Category = "Кронштейны"   },
                new() { Sku = "BR-UNI", Name = "Кронштейн универсальный металлический", Unit = "шт", Price = 70000m,  Category = "Кронштейны"   },
                new() { Sku = "ST-S",   Name = "Подставка под огнетушитель (малая)", Unit = "шт", Price = 80000m,  Category = "Подставки"     },
                new() { Sku = "ST-D",   Name = "Подставка под огнетушители двойная", Unit = "шт", Price = 120000m, Category = "Подставки"     },
                new() { Sku = "ST-FLR", Name = "Напольная стойка для огнетушителя",  Unit = "шт", Price = 180000m, Category = "Подставки"     },
                new() { Sku = "CAB-1",  Name = "Шкаф для огнетушителя (металл)",     Unit = "шт", Price = 450000m, Category = "Шкафы"         }
            };

            foreach (var p in toAdd)
            {
                if (!await db.Products.AnyAsync(x => x.Sku == p.Sku))
                {
                    db.Products.Add(p);
                    await db.SaveChangesAsync();

                    // Default stocks and batches for the new product
                    db.Stocks.Add(new ProjectApp.Api.Models.Stock { ProductId = p.Id, Register = ProjectApp.Api.Models.StockRegister.IM40, Qty = 100m });
                    db.Stocks.Add(new ProjectApp.Api.Models.Stock { ProductId = p.Id, Register = ProjectApp.Api.Models.StockRegister.ND40, Qty = 50m });
                    await db.SaveChangesAsync();

                    db.Batches.Add(new ProjectApp.Api.Models.Batch { ProductId = p.Id, Register = ProjectApp.Api.Models.StockRegister.IM40, Qty = 100m, UnitCost = 0m, CreatedAt = DateTime.UtcNow, Note = "seed" });
                    db.Batches.Add(new ProjectApp.Api.Models.Batch { ProductId = p.Id, Register = ProjectApp.Api.Models.StockRegister.ND40, Qty = 50m,  UnitCost = 0m, CreatedAt = DateTime.UtcNow, Note = "seed" });
                    await db.SaveChangesAsync();
                }
            }
        }
    }

    // 2) Create MySQL views for stock availability and manager stats (so the site can query directly if needed)
    if (provider.Contains("MySql", StringComparison.OrdinalIgnoreCase))
    {
        var createViewById = @"CREATE OR REPLACE VIEW app_available_stock AS
SELECT
  s.ProductId,
  SUM(CASE WHEN s.Register IN (0,1) THEN s.Qty ELSE 0 END) AS total_qty,
  SUM(CASE WHEN s.Register = 1 THEN s.Qty ELSE 0 END)       AS im40_qty,
  SUM(CASE WHEN s.Register = 0 THEN s.Qty ELSE 0 END)       AS nd40_qty
FROM Stocks s
GROUP BY s.ProductId;";

        var createViewBySku = @"CREATE OR REPLACE VIEW app_available_stock_sku AS
SELECT
  p.Sku,
  SUM(CASE WHEN s.Register IN (0,1) THEN s.Qty ELSE 0 END) AS total_qty,
  SUM(CASE WHEN s.Register = 1 THEN s.Qty ELSE 0 END)       AS im40_qty,
  SUM(CASE WHEN s.Register = 0 THEN s.Qty ELSE 0 END)       AS nd40_qty
FROM Stocks s
JOIN Products p ON p.Id = s.ProductId
GROUP BY p.Sku;";

        var createManagerStatsView = @"CREATE OR REPLACE VIEW app_manager_stats AS
SELECT
  COALESCE(s.CreatedBy, 'unknown') AS user_name,
  COUNT(*) AS sales_count,
  COALESCE(SUM(s.Total), 0) AS turnover
FROM Sales s
GROUP BY COALESCE(s.CreatedBy, 'unknown');";

        try
        {
            await db.Database.ExecuteSqlRawAsync(createViewById);
            await db.Database.ExecuteSqlRawAsync(createViewBySku);
            await db.Database.ExecuteSqlRawAsync(createManagerStatsView);
        }
        catch
        {
            // ignore view creation errors (e.g., insufficient privileges), app can still run
        }
    }
    else if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        var drop = "DROP VIEW IF EXISTS app_manager_stats;";
        var create = @"CREATE VIEW app_manager_stats AS
SELECT
  COALESCE(CreatedBy, 'unknown') AS user_name,
  COUNT(*) AS sales_count,
  COALESCE(SUM(Total), 0) AS turnover
FROM Sales
GROUP BY COALESCE(CreatedBy, 'unknown');";
        try
        {
            await db.Database.ExecuteSqlRawAsync(drop);
            await db.Database.ExecuteSqlRawAsync(create);
        }
        catch { }
    }
}
// ------------------------------------------------------------------------

var swaggerEnabled = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled");
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseSerilogRequestLogging();

// Middleware
app.UseCors("DefaultCors");

// Correlation ID middleware + exception handler
app.UseMiddleware<CorrelationIdMiddleware>();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var env = app.Environment;
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var ex = feature?.Error;

        var problem = new ProblemDetails
        {
            Type = "https://datatracker.ietf.org/doc/html/rfc7807",
            Title = env.IsDevelopment() ? ex?.GetType().Name ?? "Unhandled Exception" : "An unexpected error occurred",
            Detail = env.IsDevelopment() ? ex?.Message : "Please contact support with the provided correlation id.",
            Status = StatusCodes.Status500InternalServerError,
            Instance = context.Request.Path
        };

        // Attach correlation id
        if (context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var cidObj) && cidObj is string cid)
        {
            problem.Extensions["correlationId"] = cid;
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(problem);
    });
});

// Always enable Swagger UI so it's available when running via CLI
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Health endpoints
// Liveness: always 200 when process is running
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});

// Readiness: checks DB availability
app.MapHealthChecks("/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = reg => reg.Tags.Contains("ready")
});

// Redirect root to Swagger UI for convenience
app.MapGet("/", () => Results.Redirect("/swagger"));

// Controllers
app.MapControllers();

app.Run();

// Expose Program class for test host discovery
public partial class Program { }
