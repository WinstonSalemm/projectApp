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
using ProjectApp.Api.Auth;

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
            string dbPath = Path.Combine(builder.Environment.ContentRootPath, "projectapp.db");
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
                    string dbPath = Path.Combine(builder.Environment.ContentRootPath, pathVal);
                    finalConn = $"Data Source={dbPath}";
                }
                else
                {
                    finalConn = conn;
                }
            }
            else
            {
                string dbPath = Path.Combine(builder.Environment.ContentRootPath, "projectapp.db");
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
    }
    catch { }

    var seedEnabled = app.Configuration.GetValue("Seed:Enabled", true);
    if (seedEnabled && !await db.Products.AnyAsync())
    {
        db.Products.AddRange(new[]
        {
            new ProjectApp.Api.Models.Product { Id = 1, Sku = "SKU-001", Name = "Coffee Beans 1kg", Unit = "kg",  Price = 15.99m },
            new ProjectApp.Api.Models.Product { Id = 2, Sku = "SKU-002", Name = "Tea Leaves 500g",  Unit = "pkg", Price = 8.49m  },
            new ProjectApp.Api.Models.Product { Id = 3, Sku = "SKU-003", Name = "Sugar 1kg",        Unit = "kg",  Price = 2.29m  },
            new ProjectApp.Api.Models.Product { Id = 4, Sku = "SKU-004", Name = "Milk 1L",          Unit = "ltr", Price = 1.19m  },
            new ProjectApp.Api.Models.Product { Id = 5, Sku = "SKU-005", Name = "Butter 200g",      Unit = "pkg", Price = 3.79m  },
            new ProjectApp.Api.Models.Product { Id = 6, Sku = "SKU-006", Name = "Bread Loaf",       Unit = "pc",  Price = 1.99m  },
            new ProjectApp.Api.Models.Product { Id = 7, Sku = "SKU-007", Name = "Eggs (12)",        Unit = "box", Price = 2.99m  },
            new ProjectApp.Api.Models.Product { Id = 8, Sku = "SKU-008", Name = "Olive Oil 500ml",  Unit = "btl", Price = 6.49m  },
            new ProjectApp.Api.Models.Product { Id = 9, Sku = "SKU-009", Name = "Pasta 1kg",        Unit = "kg",  Price = 2.59m  },
            new ProjectApp.Api.Models.Product { Id = 10,Sku = "SKU-010", Name = "Tomato Sauce 300g",Unit = "jar", Price = 2.39m  }
        });
        await db.SaveChangesAsync();
    }

    // 2) Create MySQL views for stock availability (so the site can query directly if needed)
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

        try
        {
            await db.Database.ExecuteSqlRawAsync(createViewById);
            await db.Database.ExecuteSqlRawAsync(createViewBySku);
        }
        catch
        {
            // ignore view creation errors (e.g., insufficient privileges), app can still run
        }
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
