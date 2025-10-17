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
using ProjectApp.Api.Modules.Finance;
using ProjectApp.Api.Modules.Finance.Models;
using ProjectApp.Api.Modules.Finance.CashFlow;
using ProjectApp.Api.Modules.Finance.Forecast;
using ProjectApp.Api.Modules.Finance.Analysis;
using ProjectApp.Api.Modules.Finance.Trends;
using ProjectApp.Api.Modules.Finance.Ratios;
using ProjectApp.Api.Modules.Finance.Taxes;
using ProjectApp.Api.Modules.Finance.Export;
using ProjectApp.Api.Modules.Finance.Clients;
using ProjectApp.Api.Modules.Finance.Alerts;

var builder = WebApplication.CreateBuilder(args);

// Railway support: use PORT environment variable if available
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

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
builder.Services.AddScoped<ISalesNotifier, SalesNotifier>();
builder.Services.AddHostedService<DailySummaryHostedService>();
builder.Services.AddScoped<ProjectApp.Api.Integrations.Telegram.IReturnsNotifier, ProjectApp.Api.Integrations.Telegram.ReturnsNotifier>();
builder.Services.AddSingleton<ProjectApp.Api.Integrations.Telegram.IDebtsNotifier, ProjectApp.Api.Integrations.Telegram.DebtsNotifier>();
builder.Services.AddHostedService<ProjectApp.Api.Services.StockSnapshotHostedService>();

// Reservations
builder.Services.Configure<ProjectApp.Api.Services.ReservationsOptions>(builder.Configuration.GetSection("Reservations"));
builder.Services.AddScoped<ProjectApp.Api.Services.ReservationsService>();
builder.Services.AddHostedService<ProjectApp.Api.Services.ReservationsCleanupService>();

// Inventory services
builder.Services.AddScoped<ProjectApp.Api.Services.InventoryConsumptionService>();
builder.Services.AddHostedService<ProjectApp.Api.Services.InventoryCleanupJob>();

// Contracts service
builder.Services.AddScoped<ProjectApp.Api.Services.ContractsService>();

// Manager bonuses service
builder.Services.AddScoped<ProjectApp.Api.Services.ManagerBonusService>();

// Client classification service
builder.Services.AddScoped<ProjectApp.Api.Services.ClientClassificationService>();

// Analytics services
builder.Services.AddScoped<ProjectApp.Api.Services.ABCAnalysisService>();
builder.Services.AddScoped<ProjectApp.Api.Services.DemandForecastService>();
builder.Services.AddScoped<ProjectApp.Api.Services.PromotionService>();
builder.Services.AddScoped<ProjectApp.Api.Services.DiscountValidationService>();

// Finance module
builder.Services.Configure<FinanceSettings>(builder.Configuration.GetSection("Finance"));
// Register FinanceSettings as singleton for services that need it directly (not via IOptions)
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<FinanceSettings>>().Value);
builder.Services.AddScoped<IFinanceRepository, FinanceRepository>();
builder.Services.AddScoped<FinanceReportBuilder>();
builder.Services.AddScoped<FinanceService>();
builder.Services.AddSingleton(sp => new FinanceMetricsCalculator(sp.GetRequiredService<IOptions<FinanceSettings>>().Value));
builder.Services.AddHostedService<FinanceSnapshotJob>();
builder.Services.AddScoped<FinanceCashFlowCalculator>();
builder.Services.AddScoped<LiquidityService>();
builder.Services.AddScoped<FinanceForecastService>();
builder.Services.AddScoped<ProductAnalysisService>();
builder.Services.AddScoped<FinanceTrendCalculator>();
builder.Services.AddScoped<TaxCalculatorService>();
builder.Services.AddScoped<FinanceExportService>();
builder.Services.AddScoped<ClientFinanceReportBuilder>();
builder.Services.AddScoped<FinanceAlertService>();

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
        // Try to apply migrations for MySQL, fallback to EnsureCreated if migrations fail
        try
        {
            var canConnect = await db.Database.CanConnectAsync();
            if (canConnect)
            {
                // Check if any tables exist
                var conn = db.Database.GetDbConnection();
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE()";
                var tableCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                await conn.CloseAsync();
                
                if (tableCount == 0)
                {
                    // Empty DB - use migrations
                    await db.Database.MigrateAsync();
                }
                else
                {
                    // DB exists - ensure schema is created (idempotent)
                    await db.Database.EnsureCreatedAsync();
                }
            }
            else
            {
                await db.Database.MigrateAsync();
            }
        }
        catch
        {
            // Fallback to EnsureCreated
            await db.Database.EnsureCreatedAsync();
        }
    }
    else
    {
        // SQLite path: avoid EF Migrate(); we rely on EnsureCreated + manual patchers below
        await db.Database.EnsureCreatedAsync();
    }

    // 1.1) Minimal schema patchers (idempotent)
    try
    {
        var provider2 = db.Database.ProviderName ?? string.Empty;
        if (provider2.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            // Ensure Categories table exists
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Categories'";
                var categoriesExists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) > 0;
                
                if (!categoriesExists)
                {
                    var createCmd = conn.CreateCommand();
                    createCmd.CommandText = @"
                        CREATE TABLE IF NOT EXISTS `Categories` (
                            `Id` int NOT NULL AUTO_INCREMENT,
                            `Name` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL,
                            PRIMARY KEY (`Id`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                    ";
                    await createCmd.ExecuteNonQueryAsync();
                }
                await conn.CloseAsync();
            }
            
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

            // Ensure Batches.UnitCost exists
            var bUnitCostExists = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Batches' AND COLUMN_NAME = 'UnitCost'";
                var scalar = await cmd.ExecuteScalarAsync();
                bUnitCostExists = scalar != null && scalar != DBNull.Value && Convert.ToInt64(scalar) > 0;
            }
            if (!bUnitCostExists)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE `Batches` ADD COLUMN `UnitCost` DECIMAL(18,2) NOT NULL DEFAULT 0;");
            }

            // Ensure Batches.CreatedAt exists
            var bCreatedAtExists = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Batches' AND COLUMN_NAME = 'CreatedAt'";
                var scalar = await cmd.ExecuteScalarAsync();
                bCreatedAtExists = scalar != null && scalar != DBNull.Value && Convert.ToInt64(scalar) > 0;
            }
            if (!bCreatedAtExists)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE `Batches` ADD COLUMN `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP;");
            }

            // Ensure Batches.Note exists
            var bNoteExists = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Batches' AND COLUMN_NAME = 'Note'";
                var scalar = await cmd.ExecuteScalarAsync();
                bNoteExists = scalar != null && scalar != DBNull.Value && Convert.ToInt64(scalar) > 0;
            }
            if (!bNoteExists)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE `Batches` ADD COLUMN `Note` TEXT NULL;");
            }

            // Ensure Products.Category exists
            var prodCatExists = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Products' AND COLUMN_NAME = 'Category'";
                var scalar = await cmd.ExecuteScalarAsync();
                if (scalar != null && scalar != DBNull.Value)
                {
                    var cnt = Convert.ToInt64(scalar);
                    prodCatExists = cnt > 0;
                }
            }
            if (!prodCatExists)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE `Products` ADD COLUMN `Category` VARCHAR(128) NOT NULL DEFAULT '';");
            }
            
            // Products.GtdCode is no longer needed - removed from model

            // Categories table is already created above in the schema patchers section

            // Ensure Contracts table exists (MySQL)
            var contractsExists = false;
            using (var conn5 = db.Database.GetDbConnection())
            {
                await conn5.OpenAsync();
                await using var cmd5 = conn5.CreateCommand();
                cmd5.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Contracts'";
                var scalar5 = await cmd5.ExecuteScalarAsync();
                contractsExists = scalar5 != null && scalar5 != DBNull.Value && Convert.ToInt64(scalar5) > 0;
            }
            if (!contractsExists)
            {
                var sql5 = @"CREATE TABLE `Contracts` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `OrgName` VARCHAR(256) NOT NULL,
  `Inn` VARCHAR(32) NULL,
  `Phone` VARCHAR(32) NULL,
  `Status` INT NOT NULL,
  `CreatedAt` DATETIME(6) NOT NULL,
  `Note` VARCHAR(1024) NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
                await db.Database.ExecuteSqlRawAsync(sql5);
            }

            var contractItemsExists = false;
            using (var conn6 = db.Database.GetDbConnection())
            {
                await conn6.OpenAsync();
                await using var cmd6 = conn6.CreateCommand();
                cmd6.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ContractItems'";
                var scalar6 = await cmd6.ExecuteScalarAsync();
                contractItemsExists = scalar6 != null && scalar6 != DBNull.Value && Convert.ToInt64(scalar6) > 0;
            }
            if (!contractItemsExists)
            {
                var sql6 = @"CREATE TABLE `ContractItems` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `ContractId` INT NOT NULL,
  `ProductId` INT NULL,
  `Name` VARCHAR(256) NOT NULL,
  `Unit` VARCHAR(16) NOT NULL,
  `Qty` DECIMAL(18,3) NOT NULL,
  `UnitPrice` DECIMAL(18,2) NOT NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_ContractItems_ContractId` (`ContractId` ASC),
  CONSTRAINT `FK_ContractItems_Contracts_ContractId` FOREIGN KEY (`ContractId`) REFERENCES `Contracts` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
                await db.Database.ExecuteSqlRawAsync(sql6);
            }

            // Ensure SaleItemConsumptions table exists (MySQL)
            var sicExists = false;
            using (var conn9 = db.Database.GetDbConnection())
            {
                await conn9.OpenAsync();
                await using var cmd9 = conn9.CreateCommand();
                cmd9.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'SaleItemConsumptions'";
                var scalar9 = await cmd9.ExecuteScalarAsync();
                sicExists = scalar9 != null && scalar9 != DBNull.Value && Convert.ToInt64(scalar9) > 0;
            }
            if (!sicExists)
            {
                var sql9 = @"CREATE TABLE `SaleItemConsumptions` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `SaleItemId` INT NOT NULL,
  `BatchId` INT NOT NULL,
  `RegisterAtSale` INT NOT NULL,
  `Qty` DECIMAL(18,3) NOT NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_SaleItemConsumptions_SaleItemId` (`SaleItemId` ASC),
  INDEX `IX_SaleItemConsumptions_BatchId` (`BatchId` ASC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
                await db.Database.ExecuteSqlRawAsync(sql9);
            }

            // Ensure ReturnItemRestocks table exists (MySQL)
            var rirExists = false;
            using (var conn10 = db.Database.GetDbConnection())
            {
                await conn10.OpenAsync();
                await using var cmd10 = conn10.CreateCommand();
                cmd10.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ReturnItemRestocks'";
                var scalar10 = await cmd10.ExecuteScalarAsync();
                rirExists = scalar10 != null && scalar10 != DBNull.Value && Convert.ToInt64(scalar10) > 0;
            }
            if (!rirExists)
            {
                var sql10 = @"CREATE TABLE `ReturnItemRestocks` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `ReturnItemId` INT NOT NULL,
  `SaleItemId` INT NOT NULL,
  `BatchId` INT NOT NULL,
  `Qty` DECIMAL(18,3) NOT NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_ReturnItemRestocks_ReturnItemId` (`ReturnItemId` ASC),
  INDEX `IX_ReturnItemRestocks_SaleItemId_BatchId` (`SaleItemId` ASC, `BatchId` ASC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
                await db.Database.ExecuteSqlRawAsync(sql10);
            }

            // Ensure SalePhotos table exists (MySQL)
            var spExists = false;
            using (var conn12 = db.Database.GetDbConnection())
            {
                await conn12.OpenAsync();
                await using var cmd12 = conn12.CreateCommand();
                cmd12.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'SalePhotos'";
                var scalar12 = await cmd12.ExecuteScalarAsync();
                spExists = scalar12 != null && scalar12 != DBNull.Value && Convert.ToInt64(scalar12) > 0;
            }
            if (!spExists)
            {
                var sql12 = @"CREATE TABLE `SalePhotos` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `SaleId` INT NOT NULL,
  `UserName` VARCHAR(64) NULL,
  `Mime` VARCHAR(64) NULL,
  `Size` BIGINT NOT NULL,
  `CreatedAt` DATETIME(6) NOT NULL,
  `PathOrBlob` VARCHAR(512) NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_SalePhotos_SaleId` (`SaleId` ASC),
  INDEX `IX_SalePhotos_UserName` (`UserName` ASC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
                await db.Database.ExecuteSqlRawAsync(sql12);
            }

            // Ensure StockSnapshots table exists (MySQL)
            var ssExists = false;
            using (var connSS = db.Database.GetDbConnection())
            {
                await connSS.OpenAsync();
                await using var cmdSS = connSS.CreateCommand();
                cmdSS.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockSnapshots'";
                var scalarSS = await cmdSS.ExecuteScalarAsync();
                ssExists = scalarSS != null && scalarSS != DBNull.Value && Convert.ToInt64(scalarSS) > 0;
            }
            if (!ssExists)
            {
                var sqlSS = @"CREATE TABLE `StockSnapshots` (
  `Id` INT NOT NULL AUTO_INCREMENT,
  `ProductId` INT NOT NULL,
  `NdQty` DECIMAL(18,3) NOT NULL,
  `ImQty` DECIMAL(18,3) NOT NULL,
  `TotalQty` DECIMAL(18,3) NOT NULL,
  `CreatedAt` DATETIME(6) NOT NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_StockSnapshots_CreatedAt` (`CreatedAt` ASC),
  INDEX `IX_StockSnapshots_ProductId` (`ProductId` ASC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
                await db.Database.ExecuteSqlRawAsync(sqlSS);
            }

            // Ensure Sales.ReservationNotes exists (MySQL)
            var salesResNotesExists = false;
            using (var conn11 = db.Database.GetDbConnection())
            {
                await conn11.OpenAsync();
                await using var cmd11 = conn11.CreateCommand();
                cmd11.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Sales' AND COLUMN_NAME = 'ReservationNotes'";
                var scalar11 = await cmd11.ExecuteScalarAsync();
                salesResNotesExists = scalar11 != null && scalar11 != DBNull.Value && Convert.ToInt64(scalar11) > 0;
            }
            if (!salesResNotesExists)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE `Sales` ADD COLUMN `ReservationNotes` TEXT NULL;");
            }

            // GtdCode column is already created above in the schema patchers section

            // Ensure Batches.GtdCode exists
            var bGtdExists = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Batches' AND COLUMN_NAME = 'GtdCode'";
                var scalar = await cmd.ExecuteScalarAsync();
                bGtdExists = scalar != null && scalar != DBNull.Value && Convert.ToInt64(scalar) > 0;
            }
            if (!bGtdExists)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE `Batches` ADD COLUMN `GtdCode` VARCHAR(64) NULL;");
            }

            // Ensure Batches.ArchivedAt exists
            var bArchivedAtExists = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Batches' AND COLUMN_NAME = 'ArchivedAt'";
                var scalar = await cmd.ExecuteScalarAsync();
                bArchivedAtExists = scalar != null && scalar != DBNull.Value && Convert.ToInt64(scalar) > 0;
            }
            if (!bArchivedAtExists)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE `Batches` ADD COLUMN `ArchivedAt` DATETIME(6) NULL;");
            }

            // Ensure StockSnapshots.TotalValue exists
            var ssValExists = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'StockSnapshots' AND COLUMN_NAME = 'TotalValue'";
                var scalar = await cmd.ExecuteScalarAsync();
                ssValExists = scalar != null && scalar != DBNull.Value && Convert.ToInt64(scalar) > 0;
            }
            if (!ssValExists)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE `StockSnapshots` ADD COLUMN `TotalValue` DECIMAL(18,2) NOT NULL DEFAULT 0;");
            }

            // Ensure InventoryTransactions table exists
            var invTrExists = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'InventoryTransactions'";
                var scalar = await cmd.ExecuteScalarAsync();
                invTrExists = scalar != null && scalar != DBNull.Value && Convert.ToInt64(scalar) > 0;
            }
            if (!invTrExists)
            {
                var sql = @"CREATE TABLE `InventoryTransactions` (
  `Id` BIGINT NOT NULL AUTO_INCREMENT,
  `ProductId` INT NOT NULL,
  `Register` INT NOT NULL,
  `Type` INT NOT NULL,
  `Qty` DECIMAL(18,3) NOT NULL,
  `UnitCost` DECIMAL(18,2) NOT NULL,
  `BatchId` INT NULL,
  `SaleId` INT NULL,
  `ReturnId` INT NULL,
  `ReservationId` INT NULL,
  `CreatedAt` DATETIME(6) NOT NULL,
  `CreatedBy` VARCHAR(64) NULL,
  `Note` VARCHAR(512) NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_InvTr_Product_Register_CreatedAt` (`ProductId` ASC, `Register` ASC, `CreatedAt` ASC),
  INDEX `IX_InvTr_SaleId` (`SaleId` ASC),
  INDEX `IX_InvTr_ReturnId` (`ReturnId` ASC),
  INDEX `IX_InvTr_ReservationId` (`ReservationId` ASC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
                await db.Database.ExecuteSqlRawAsync(sql);
            }

            // Ensure InventoryConsumptions table exists
            var invConsExists = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'InventoryConsumptions'";
                var scalar = await cmd.ExecuteScalarAsync();
                invConsExists = scalar != null && scalar != DBNull.Value && Convert.ToInt64(scalar) > 0;
            }
            if (!invConsExists)
            {
                var sql = @"CREATE TABLE `InventoryConsumptions` (
  `Id` BIGINT NOT NULL AUTO_INCREMENT,
  `ProductId` INT NOT NULL,
  `BatchId` INT NOT NULL,
  `Register` INT NOT NULL,
  `Qty` DECIMAL(18,3) NOT NULL,
  `SaleItemId` INT NULL,
  `ReturnItemId` INT NULL,
  `CreatedAt` DATETIME(6) NOT NULL,
  `CreatedBy` VARCHAR(64) NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_InvCons_Product_Batch` (`ProductId` ASC, `BatchId` ASC),
  INDEX `IX_InvCons_SaleItemId` (`SaleItemId` ASC),
  INDEX `IX_InvCons_ReturnItemId` (`ReturnItemId` ASC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
                await db.Database.ExecuteSqlRawAsync(sql);
            }

            // Ensure ProductCostHistories table exists
            var pchExists = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ProductCostHistories'";
                var scalar = await cmd.ExecuteScalarAsync();
                pchExists = scalar != null && scalar != DBNull.Value && Convert.ToInt64(scalar) > 0;
            }
            if (!pchExists)
            {
                var sql = @"CREATE TABLE `ProductCostHistories` (
  `Id` BIGINT NOT NULL AUTO_INCREMENT,
  `ProductId` INT NOT NULL,
  `UnitCost` DECIMAL(18,2) NOT NULL,
  `SnapshotAt` DATETIME(6) NOT NULL,
  `Note` VARCHAR(256) NULL,
  PRIMARY KEY (`Id`),
  INDEX `IX_PCH_Product_SnapshotAt` (`ProductId` ASC, `SnapshotAt` ASC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
                await db.Database.ExecuteSqlRawAsync(sql);
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

            // Ensure Batches.UnitCost exists
            var hasBUnitCost = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('Batches');";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(1);
                    if (string.Equals(name, "UnitCost", StringComparison.OrdinalIgnoreCase)) { hasBUnitCost = true; break; }
                }
            }
            if (!hasBUnitCost)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Batches ADD COLUMN UnitCost DECIMAL(18,2) NOT NULL DEFAULT 0;");
            }

            // Ensure Batches.CreatedAt exists
            var hasBCreatedAt = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('Batches');";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(1);
                    if (string.Equals(name, "CreatedAt", StringComparison.OrdinalIgnoreCase)) { hasBCreatedAt = true; break; }
                }
            }
            if (!hasBCreatedAt)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Batches ADD COLUMN CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP;");
            }

            // Ensure Batches.Note exists
            var hasBNote = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('Batches');";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(1);
                    if (string.Equals(name, "Note", StringComparison.OrdinalIgnoreCase)) { hasBNote = true; break; }
                }
            }
            if (!hasBNote)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Batches ADD COLUMN Note TEXT NULL;");
            }

            // Ensure Products.Category exists
            var hasProdCat = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('Products');";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(1);
                    if (string.Equals(name, "Category", StringComparison.OrdinalIgnoreCase)) { hasProdCat = true; break; }
                }
            }
            if (!hasProdCat)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Products ADD COLUMN Category TEXT NOT NULL DEFAULT '';");
            }

            // Ensure Categories table exists (SQLite)
            var hasCategories = false;
            using (var connC = db.Database.GetDbConnection())
            {
                await connC.OpenAsync();
                await using var cmdC = connC.CreateCommand();
                cmdC.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Categories'";
                var scalarC = await cmdC.ExecuteScalarAsync();
                hasCategories = scalarC != null && scalarC != DBNull.Value && Convert.ToInt64(scalarC) > 0;
            }
            if (!hasCategories)
            {
                var sqlC = @"CREATE TABLE Categories (
  Id INTEGER NOT NULL CONSTRAINT PK_Categories PRIMARY KEY AUTOINCREMENT,
  Name TEXT NOT NULL UNIQUE
);";
                await db.Database.ExecuteSqlRawAsync(sqlC);
            }

            // Ensure SalePhotos table exists (SQLite)
            var hasSalePhotos = false;
            using (var connSP = db.Database.GetDbConnection())
            {
                await connSP.OpenAsync();
                await using var cmdSP = connSP.CreateCommand();
                cmdSP.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SalePhotos'";
                var scalarSP = await cmdSP.ExecuteScalarAsync();
                hasSalePhotos = scalarSP != null && scalarSP != DBNull.Value && Convert.ToInt64(scalarSP) > 0;
            }
            if (!hasSalePhotos)
            {
                var sqlSP = @"CREATE TABLE SalePhotos (
  Id INTEGER NOT NULL CONSTRAINT PK_SalePhotos PRIMARY KEY AUTOINCREMENT,
  SaleId INTEGER NOT NULL,
  UserName TEXT NULL,
  Mime TEXT NULL,
  Size INTEGER NOT NULL,
  CreatedAt TEXT NOT NULL,
  PathOrBlob TEXT NULL
);";
                await db.Database.ExecuteSqlRawAsync(sqlSP);
            }

            // Ensure StockSnapshots table exists (SQLite)
            var hasStockSnapshots = false;
            using (var connSS = db.Database.GetDbConnection())
            {
                await connSS.OpenAsync();
                await using var cmdSS = connSS.CreateCommand();
                cmdSS.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='StockSnapshots'";
                var scalarSS = await cmdSS.ExecuteScalarAsync();
                hasStockSnapshots = scalarSS != null && scalarSS != DBNull.Value && Convert.ToInt64(scalarSS) > 0;
            }
            if (!hasStockSnapshots)
            {
                var sqlSS = @"CREATE TABLE StockSnapshots (
  Id INTEGER NOT NULL CONSTRAINT PK_StockSnapshots PRIMARY KEY AUTOINCREMENT,
  ProductId INTEGER NOT NULL,
  NdQty DECIMAL(18,3) NOT NULL,
  ImQty DECIMAL(18,3) NOT NULL,
  TotalQty DECIMAL(18,3) NOT NULL,
  CreatedAt TEXT NOT NULL
);";
                await db.Database.ExecuteSqlRawAsync(sqlSS);
            }

            // Ensure Reservations tables exist (SQLite)
            var hasReservations = false;
            using (var connR = db.Database.GetDbConnection())
            {
                await connR.OpenAsync();
                await using var cmdR = connR.CreateCommand();
                cmdR.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Reservations'";
                var scalarR = await cmdR.ExecuteScalarAsync();
                hasReservations = scalarR != null && scalarR != DBNull.Value && Convert.ToInt64(scalarR) > 0;
            }
            if (!hasReservations)
            {
                var sqlR = @"CREATE TABLE Reservations (
  Id INTEGER NOT NULL CONSTRAINT PK_Reservations PRIMARY KEY AUTOINCREMENT,
  ClientId INTEGER NULL,
  SaleId INTEGER NULL,
  ContractId INTEGER NULL,
  CreatedBy TEXT NOT NULL,
  CreatedAt TEXT NOT NULL,
  Paid INTEGER NOT NULL,
  ReservedUntil TEXT NOT NULL,
  Status INTEGER NOT NULL,
  Note TEXT NULL,
  PhotoPath TEXT NULL,
  PhotoMime TEXT NULL,
  PhotoSize INTEGER NULL,
  PhotoCreatedAt TEXT NULL
);";
                await db.Database.ExecuteSqlRawAsync(sqlR);
            }

            var hasReservationItems = false;
            using (var connRI = db.Database.GetDbConnection())
            {
                await connRI.OpenAsync();
                await using var cmdRI = connRI.CreateCommand();
                cmdRI.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ReservationItems'";
                var scalarRI = await cmdRI.ExecuteScalarAsync();
                hasReservationItems = scalarRI != null && scalarRI != DBNull.Value && Convert.ToInt64(scalarRI) > 0;
            }
            if (!hasReservationItems)
            {
                var sqlRI = @"CREATE TABLE ReservationItems (
  Id INTEGER NOT NULL CONSTRAINT PK_ReservationItems PRIMARY KEY AUTOINCREMENT,
  ReservationId INTEGER NOT NULL,
  ProductId INTEGER NOT NULL,
  Register INTEGER NOT NULL,
  Qty DECIMAL(18,3) NOT NULL,
  Sku TEXT NOT NULL,
  Name TEXT NOT NULL,
  UnitPrice DECIMAL(18,2) NOT NULL,
  CONSTRAINT FK_ReservationItems_Reservations_ReservationId FOREIGN KEY (ReservationId) REFERENCES Reservations (Id) ON DELETE CASCADE
);";
                await db.Database.ExecuteSqlRawAsync(sqlRI);
            }

            var hasReservationLogs = false;
            using (var connRL = db.Database.GetDbConnection())
            {
                await connRL.OpenAsync();
                await using var cmdRL = connRL.CreateCommand();
                cmdRL.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ReservationLogs'";
                var scalarRL = await cmdRL.ExecuteScalarAsync();
                hasReservationLogs = scalarRL != null && scalarRL != DBNull.Value && Convert.ToInt64(scalarRL) > 0;
            }
            if (!hasReservationLogs)
            {
                var sqlRL = @"CREATE TABLE ReservationLogs (
  Id INTEGER NOT NULL CONSTRAINT PK_ReservationLogs PRIMARY KEY AUTOINCREMENT,
  ReservationId INTEGER NOT NULL,
  Action TEXT NOT NULL,
  UserName TEXT NOT NULL,
  CreatedAt TEXT NOT NULL,
  Details TEXT NULL
);";
                await db.Database.ExecuteSqlRawAsync(sqlRL);
            }

            // Ensure Contracts table exists (SQLite)
            var hasContracts = false;
            using (var conn7 = db.Database.GetDbConnection())
            {
                await conn7.OpenAsync();
                await using var cmd7 = conn7.CreateCommand();
                cmd7.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Contracts'";
                var scalar7 = await cmd7.ExecuteScalarAsync();
                hasContracts = scalar7 != null && scalar7 != DBNull.Value && Convert.ToInt64(scalar7) > 0;
            }
            if (!hasContracts)
            {
                var sql7 = @"CREATE TABLE Contracts (
  Id INTEGER NOT NULL CONSTRAINT PK_Contracts PRIMARY KEY AUTOINCREMENT,
  OrgName TEXT NOT NULL,
  Inn TEXT NULL,
  Phone TEXT NULL,
  Status INTEGER NOT NULL,
  CreatedAt TEXT NOT NULL,
  Note TEXT NULL
);";
                await db.Database.ExecuteSqlRawAsync(sql7);
            }

            var hasContractItems = false;
            using (var conn8 = db.Database.GetDbConnection())
            {
                await conn8.OpenAsync();
                await using var cmd8 = conn8.CreateCommand();
                cmd8.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ContractItems'";
                var scalar8 = await cmd8.ExecuteScalarAsync();
                hasContractItems = scalar8 != null && scalar8 != DBNull.Value && Convert.ToInt64(scalar8) > 0;
            }
            if (!hasContractItems)
            {
                var sql8 = @"CREATE TABLE ContractItems (
  Id INTEGER NOT NULL CONSTRAINT PK_ContractItems PRIMARY KEY AUTOINCREMENT,
  ContractId INTEGER NOT NULL,
  ProductId INTEGER NULL,
  Name TEXT NOT NULL,
  Unit TEXT NOT NULL,
  Qty DECIMAL(18,3) NOT NULL,
  UnitPrice DECIMAL(18,2) NOT NULL,
  CONSTRAINT FK_ContractItems_Contracts_ContractId FOREIGN KEY (ContractId) REFERENCES Contracts (Id) ON DELETE CASCADE
);";
                await db.Database.ExecuteSqlRawAsync(sql8);
            }

            // Ensure SaleItemConsumptions table exists (SQLite)
            var hasSic = false;
            using (var conn9 = db.Database.GetDbConnection())
            {
                await conn9.OpenAsync();
                await using var cmd9 = conn9.CreateCommand();
                cmd9.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SaleItemConsumptions'";
                var scalar9 = await cmd9.ExecuteScalarAsync();
                hasSic = scalar9 != null && scalar9 != DBNull.Value && Convert.ToInt64(scalar9) > 0;
            }
            if (!hasSic)
            {
                var sql9 = @"CREATE TABLE SaleItemConsumptions (
  Id INTEGER NOT NULL CONSTRAINT PK_SaleItemConsumptions PRIMARY KEY AUTOINCREMENT,
  SaleItemId INTEGER NOT NULL,
  BatchId INTEGER NOT NULL,
  RegisterAtSale INTEGER NOT NULL,
  Qty DECIMAL(18,3) NOT NULL
);";
                await db.Database.ExecuteSqlRawAsync(sql9);
            }

            // Ensure ReturnItemRestocks table exists (SQLite)
            var hasRir = false;
            using (var conn10 = db.Database.GetDbConnection())
            {
                await conn10.OpenAsync();
                await using var cmd10 = conn10.CreateCommand();
                cmd10.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ReturnItemRestocks'";
                var scalar10 = await cmd10.ExecuteScalarAsync();
                hasRir = scalar10 != null && scalar10 != DBNull.Value && Convert.ToInt64(scalar10) > 0;
            }
            if (!hasRir)
            {
                var sql10 = @"CREATE TABLE ReturnItemRestocks (
  Id INTEGER NOT NULL CONSTRAINT PK_ReturnItemRestocks PRIMARY KEY AUTOINCREMENT,
  ReturnItemId INTEGER NOT NULL,
  SaleItemId INTEGER NOT NULL,
  BatchId INTEGER NOT NULL,
  Qty DECIMAL(18,3) NOT NULL
);";
                await db.Database.ExecuteSqlRawAsync(sql10);
            }

            // Ensure Sales.ReservationNotes exists (SQLite)
            var hasSalesResNotes = false;
            using (var conn11 = db.Database.GetDbConnection())
            {
                await conn11.OpenAsync();
                await using var cmd11 = conn11.CreateCommand();
                cmd11.CommandText = "PRAGMA table_info('Sales');";
                await using var reader11 = await cmd11.ExecuteReaderAsync();
                while (await reader11.ReadAsync())
                {
                    var name = reader11.GetString(1);
                    if (string.Equals(name, "ReservationNotes", StringComparison.OrdinalIgnoreCase)) { hasSalesResNotes = true; break; }
                }
            }
            if (!hasSalesResNotes)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Sales ADD COLUMN ReservationNotes TEXT NULL;");
            }

            // Ensure Products.GtdCode exists
            var hasProdGtd = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('Products');";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(1);
                    if (string.Equals(name, "GtdCode", StringComparison.OrdinalIgnoreCase)) { hasProdGtd = true; break; }
                }
            }
            if (!hasProdGtd)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Products ADD COLUMN GtdCode TEXT NULL;");
            }

            // Ensure Batches.GtdCode exists
            var hasBGtd = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('Batches');";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(1);
                    if (string.Equals(name, "GtdCode", StringComparison.OrdinalIgnoreCase)) { hasBGtd = true; break; }
                }
            }
            if (!hasBGtd)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Batches ADD COLUMN GtdCode TEXT NULL;");
            }

            // Ensure Batches.ArchivedAt exists
            var hasBArchivedAt = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('Batches');";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(1);
                    if (string.Equals(name, "ArchivedAt", StringComparison.OrdinalIgnoreCase)) { hasBArchivedAt = true; break; }
                }
            }
            if (!hasBArchivedAt)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Batches ADD COLUMN ArchivedAt TEXT NULL;");
            }

            // Ensure StockSnapshots.TotalValue exists
            var hasSSTotalValue = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('StockSnapshots');";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(1);
                    if (string.Equals(name, "TotalValue", StringComparison.OrdinalIgnoreCase)) { hasSSTotalValue = true; break; }
                }
            }
            if (!hasSSTotalValue)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE StockSnapshots ADD COLUMN TotalValue DECIMAL(18,2) NOT NULL DEFAULT 0;");
            }

            // Ensure InventoryTransactions table exists
            var hasInvTr = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='InventoryTransactions'";
                var scalar = await cmd.ExecuteScalarAsync();
                hasInvTr = scalar != null && scalar != DBNull.Value && Convert.ToInt64(scalar) > 0;
            }
            if (!hasInvTr)
            {
                var sql = @"CREATE TABLE InventoryTransactions (
  Id INTEGER NOT NULL CONSTRAINT PK_InventoryTransactions PRIMARY KEY AUTOINCREMENT,
  ProductId INTEGER NOT NULL,
  Register INTEGER NOT NULL,
  Type INTEGER NOT NULL,
  Qty DECIMAL(18,3) NOT NULL,
  UnitCost DECIMAL(18,2) NOT NULL,
  BatchId INTEGER NULL,
  SaleId INTEGER NULL,
  ReturnId INTEGER NULL,
  ReservationId INTEGER NULL,
  CreatedAt TEXT NOT NULL,
  CreatedBy TEXT NULL,
  Note TEXT NULL
);";
                await db.Database.ExecuteSqlRawAsync(sql);
            }

            // Ensure InventoryConsumptions table exists
            var hasInvCons = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='InventoryConsumptions'";
                var scalar = await cmd.ExecuteScalarAsync();
                hasInvCons = scalar != null && scalar != DBNull.Value && Convert.ToInt64(scalar) > 0;
            }
            if (!hasInvCons)
            {
                var sql = @"CREATE TABLE InventoryConsumptions (
  Id INTEGER NOT NULL CONSTRAINT PK_InventoryConsumptions PRIMARY KEY AUTOINCREMENT,
  ProductId INTEGER NOT NULL,
  BatchId INTEGER NOT NULL,
  Register INTEGER NOT NULL,
  Qty DECIMAL(18,3) NOT NULL,
  SaleItemId INTEGER NULL,
  ReturnItemId INTEGER NULL,
  CreatedAt TEXT NOT NULL,
  CreatedBy TEXT NULL
);";
                await db.Database.ExecuteSqlRawAsync(sql);
            }

            // Ensure ProductCostHistories table exists
            var hasPch = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ProductCostHistories'";
                var scalar = await cmd.ExecuteScalarAsync();
                hasPch = scalar != null && scalar != DBNull.Value && Convert.ToInt64(scalar) > 0;
            }
            if (!hasPch)
            {
                var sql = @"CREATE TABLE ProductCostHistories (
  Id INTEGER NOT NULL CONSTRAINT PK_ProductCostHistories PRIMARY KEY AUTOINCREMENT,
  ProductId INTEGER NOT NULL,
  UnitCost DECIMAL(18,2) NOT NULL,
  SnapshotAt TEXT NOT NULL,
  Note TEXT NULL
);";
                await db.Database.ExecuteSqlRawAsync(sql);
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
  `OwnedSalesCount` INT NOT NULL,
  `OwnedTurnover` DECIMAL(18,2) NOT NULL,
  PRIMARY KEY (`UserName`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE utf8mb4_unicode_ci;";
                await db.Database.ExecuteSqlRawAsync(sql);
            }
            else
            {
                // Patch: add owned columns if missing
                bool hasOwnedSales = false, hasOwnedTurnover = false;
                using (var conn = db.Database.GetDbConnection())
                {
                    await conn.OpenAsync();
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ManagerStats'";
                    var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync()) cols.Add(reader.GetString(0));
                    hasOwnedSales = cols.Contains("OwnedSalesCount");
                    hasOwnedTurnover = cols.Contains("OwnedTurnover");
                }
                if (!hasOwnedSales)
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE `ManagerStats` ADD COLUMN `OwnedSalesCount` INT NOT NULL DEFAULT 0;");
                if (!hasOwnedTurnover)
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE `ManagerStats` ADD COLUMN `OwnedTurnover` DECIMAL(18,2) NOT NULL DEFAULT 0;");
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
  Turnover DECIMAL(18,2) NOT NULL,
  OwnedSalesCount INTEGER NOT NULL,
  OwnedTurnover DECIMAL(18,2) NOT NULL
);";
                await db.Database.ExecuteSqlRawAsync(sql);
            }
            else
            {
                // Patch: add owned columns if missing
                bool hasOwnedSales = false, hasOwnedTurnover = false;
                using (var conn = db.Database.GetDbConnection())
                {
                    await conn.OpenAsync();
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = "PRAGMA table_info('ManagerStats');";
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var name = reader.GetString(1);
                        if (string.Equals(name, "OwnedSalesCount", StringComparison.OrdinalIgnoreCase)) hasOwnedSales = true;
                        if (string.Equals(name, "OwnedTurnover", StringComparison.OrdinalIgnoreCase)) hasOwnedTurnover = true;
                    }
                }
                if (!hasOwnedSales)
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE ManagerStats ADD COLUMN OwnedSalesCount INTEGER NOT NULL DEFAULT 0;");
                if (!hasOwnedTurnover)
                    await db.Database.ExecuteSqlRawAsync("ALTER TABLE ManagerStats ADD COLUMN OwnedTurnover DECIMAL(18,2) NOT NULL DEFAULT 0;");
            }
        }
    }
    catch { }

    // 1.3.1) Quick critical ensures (SQLite) independent of the big patcher above
    try
    {
        var providerQ = db.Database.ProviderName ?? string.Empty;
        if (providerQ.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            bool hasBatches = false;
            using (var connB = db.Database.GetDbConnection())
            {
                await connB.OpenAsync();
                await using var cmdB = connB.CreateCommand();
                cmdB.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Batches'";
                var scalarB = await cmdB.ExecuteScalarAsync();
                hasBatches = scalarB != null && scalarB != DBNull.Value && Convert.ToInt64(scalarB) > 0;
            }
            if (!hasBatches)
            {
                try
                {
                    var sqlB = @"CREATE TABLE Batches (
  Id INTEGER NOT NULL CONSTRAINT PK_Batches PRIMARY KEY AUTOINCREMENT,
  ProductId INTEGER NOT NULL,
  Register INTEGER NOT NULL,
  Qty DECIMAL(18,3) NOT NULL,
  UnitCost DECIMAL(18,2) NOT NULL DEFAULT 0,
  CreatedAt TEXT NOT NULL,
  Note TEXT NULL,
  Code TEXT NULL
);";
                    await db.Database.ExecuteSqlRawAsync(sqlB);
                }
                catch { }
            }

            bool hasStocks = false;
            using (var connS = db.Database.GetDbConnection())
            {
                await connS.OpenAsync();
                await using var cmdS = connS.CreateCommand();
                cmdS.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Stocks'";
                var scalarS = await cmdS.ExecuteScalarAsync();
                hasStocks = scalarS != null && scalarS != DBNull.Value && Convert.ToInt64(scalarS) > 0;
            }
            if (!hasStocks)
            {
                try
                {
                    var sqlS = @"CREATE TABLE Stocks (
  Id INTEGER NOT NULL CONSTRAINT PK_Stocks PRIMARY KEY AUTOINCREMENT,
  ProductId INTEGER NOT NULL,
  Register INTEGER NOT NULL,
  Qty DECIMAL(18,3) NOT NULL
);";
                    await db.Database.ExecuteSqlRawAsync(sqlS);
                }
                catch { }
            }

            // Ensure Products.Category exists (needed for seeding below)
            bool hasProdCategory = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('Products');";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(1);
                    if (string.Equals(name, "Category", StringComparison.OrdinalIgnoreCase)) { hasProdCategory = true; break; }
                }
            }
            if (!hasProdCategory)
            {
                try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Products ADD COLUMN Category TEXT NOT NULL DEFAULT ''; "); } catch { }
            }

            // Ensure Reservations tables exist for cleanup service
            bool hasRes = false;
            using (var connR = db.Database.GetDbConnection())
            {
                await connR.OpenAsync();
                await using var cmdR = connR.CreateCommand();
                cmdR.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Reservations'";
                var scalarR = await cmdR.ExecuteScalarAsync();
                hasRes = scalarR != null && scalarR != DBNull.Value && Convert.ToInt64(scalarR) > 0;
            }
            if (!hasRes)
            {
                try
                {
                    var sqlR = @"CREATE TABLE Reservations (
  Id INTEGER NOT NULL CONSTRAINT PK_Reservations PRIMARY KEY AUTOINCREMENT,
  ClientId INTEGER NULL,
  SaleId INTEGER NULL,
  ContractId INTEGER NULL,
  CreatedBy TEXT NOT NULL,
  CreatedAt TEXT NOT NULL,
  Paid INTEGER NOT NULL,
  ReservedUntil TEXT NOT NULL,
  Status INTEGER NOT NULL,
  Note TEXT NULL,
  PhotoPath TEXT NULL,
  PhotoMime TEXT NULL,
  PhotoSize INTEGER NULL,
  PhotoCreatedAt TEXT NULL
);";
                    await db.Database.ExecuteSqlRawAsync(sqlR);
                }
                catch { }
            }

            bool hasResItems = false;
            using (var connRI = db.Database.GetDbConnection())
            {
                await connRI.OpenAsync();
                await using var cmdRI = connRI.CreateCommand();
                cmdRI.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ReservationItems'";
                var scalarRI = await cmdRI.ExecuteScalarAsync();
                hasResItems = scalarRI != null && scalarRI != DBNull.Value && Convert.ToInt64(scalarRI) > 0;
            }
            if (!hasResItems)
            {
                try
                {
                    var sqlRI = @"CREATE TABLE ReservationItems (
  Id INTEGER NOT NULL CONSTRAINT PK_ReservationItems PRIMARY KEY AUTOINCREMENT,
  ReservationId INTEGER NOT NULL,
  ProductId INTEGER NOT NULL,
  Register INTEGER NOT NULL,
  Qty DECIMAL(18,3) NOT NULL,
  Sku TEXT NOT NULL,
  Name TEXT NOT NULL,
  UnitPrice DECIMAL(18,2) NOT NULL,
  CONSTRAINT FK_ReservationItems_Reservations_ReservationId FOREIGN KEY (ReservationId) REFERENCES Reservations (Id) ON DELETE CASCADE
);";
                    await db.Database.ExecuteSqlRawAsync(sqlRI);
                }
                catch { }
            }

            bool hasResLogs = false;
            using (var connRL = db.Database.GetDbConnection())
            {
                await connRL.OpenAsync();
                await using var cmdRL = connRL.CreateCommand();
                cmdRL.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ReservationLogs'";
                var scalarRL = await cmdRL.ExecuteScalarAsync();
                hasResLogs = scalarRL != null && scalarRL != DBNull.Value && Convert.ToInt64(scalarRL) > 0;
            }
            if (!hasResLogs)
            {
                try
                {
                    var sqlRL = @"CREATE TABLE ReservationLogs (
  Id INTEGER NOT NULL CONSTRAINT PK_ReservationLogs PRIMARY KEY AUTOINCREMENT,
  ReservationId INTEGER NOT NULL,
  Action TEXT NOT NULL,
  UserName TEXT NOT NULL,
  CreatedAt TEXT NOT NULL,
  Details TEXT NULL
);";
                    await db.Database.ExecuteSqlRawAsync(sqlRL);
                }
                catch { }
            }
        }
    }
    catch { }

    // 1.2.2) Ensure Clients table has Type, OwnerUserName, CreatedAt
    try
    {
        var provider5 = db.Database.ProviderName ?? string.Empty;
        if (provider5.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            bool hasType = false, hasOwner = false, hasCreatedAt = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Clients'";
                await using var reader = await cmd.ExecuteReaderAsync();
                var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (await reader.ReadAsync()) cols.Add(reader.GetString(0));
                hasType = cols.Contains("Type");
                hasOwner = cols.Contains("OwnerUserName");
                hasCreatedAt = cols.Contains("CreatedAt");
            }
            if (!hasType)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE `Clients` ADD COLUMN `Type` INT NOT NULL DEFAULT 1;");
            if (!hasOwner)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE `Clients` ADD COLUMN `OwnerUserName` VARCHAR(64) NULL;");
            if (!hasCreatedAt)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE `Clients` ADD COLUMN `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP;");
        }
        else if (provider5.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            bool hasType = false, hasOwner = false, hasCreatedAt = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('Clients');";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(1);
                    if (string.Equals(name, "Type", StringComparison.OrdinalIgnoreCase)) hasType = true;
                    if (string.Equals(name, "OwnerUserName", StringComparison.OrdinalIgnoreCase)) hasOwner = true;
                    if (string.Equals(name, "CreatedAt", StringComparison.OrdinalIgnoreCase)) hasCreatedAt = true;
                }
            }
            if (!hasType)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Clients ADD COLUMN Type INTEGER NOT NULL DEFAULT 1;");
            if (!hasOwner)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Clients ADD COLUMN OwnerUserName TEXT NULL;");
            if (!hasCreatedAt)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Clients ADD COLUMN CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP;");
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

    // 1.4) Seed initial fire-safety products if missing (idempotent by SKU)
    try
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
    catch { }

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
