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
using ProjectApp.Api.Middleware;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

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
    // Apply requirement only to [Authorize] endpoints via operation filter
    c.OperationFilter<ProjectApp.Api.Swagger.AuthorizeCheckOperationFilter>();
});

// EF Core - SQLite (ensure DB file is under ContentRoot)
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection");
    string finalConn;

    if (string.IsNullOrWhiteSpace(conn))
    {
        // No connection string => use ContentRoot anchored file
        string dbPath = Path.Combine(builder.Environment.ContentRootPath, "projectapp.db");
        finalConn = $"Data Source={dbPath}";
    }
    else
    {
        // Try to extract Data Source path and anchor if relative
        var parts = conn.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string? dataSourcePart = parts.FirstOrDefault(p => p.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) || p.StartsWith("DataSource=", StringComparison.OrdinalIgnoreCase));
        if (dataSourcePart is not null)
        {
            var kv = dataSourcePart.Split('=');
            var pathVal = kv.Length > 1 ? kv[1] : string.Empty;
            if (!string.IsNullOrWhiteSpace(pathVal) && !Path.IsPathRooted(pathVal))
            {
                // Relative => anchor to ContentRoot
                string dbPath = Path.Combine(builder.Environment.ContentRootPath, pathVal);
                finalConn = $"Data Source={dbPath}";
            }
            else
            {
                // Already rooted or empty => use provided connection string as-is
                finalConn = conn;
            }
        }
        else
        {
            // No Data Source key present => fall back to ContentRoot
            string dbPath = Path.Combine(builder.Environment.ContentRootPath, "projectapp.db");
            finalConn = $"Data Source={dbPath}";
        }
    }

    // Normalize to avoid DB in Temp directories when relative paths are used
    options.UseSqlite(finalConn);
});

// DI
builder.Services.AddScoped<IProductRepository, EfProductRepository>();
builder.Services.AddScoped<ISaleRepository, EfSaleRepository>();
builder.Services.AddScoped<ISaleCalculator, SaleCalculator>();

// Authentication & Authorization
builder.Services.AddAuthentication(ApiKeyAuthenticationOptions.DefaultScheme)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.DefaultScheme, _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireApiKey", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.AddAuthenticationSchemes(ApiKeyAuthenticationOptions.DefaultScheme);
    });
});

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("db", tags: new[] { "ready" });

var app = builder.Build();

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
app.UseSwagger();
app.UseSwaggerUI();

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

// Development-only: auto-apply migrations and optional seed
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    var seedEnabled = app.Configuration.GetValue("Seed:Enabled", true);
    if (seedEnabled)
    {
        // Seed minimal products if empty
        if (!db.Products.Any())
        {
            db.Products.AddRange(new[]
            {
                new ProjectApp.Api.Models.Product { Id = 1, Sku = "SKU-001", Name = "Coffee Beans 1kg", Unit = "kg", Price = 15.99m },
                new ProjectApp.Api.Models.Product { Id = 2, Sku = "SKU-002", Name = "Tea Leaves 500g", Unit = "pkg", Price = 8.49m },
                new ProjectApp.Api.Models.Product { Id = 3, Sku = "SKU-003", Name = "Sugar 1kg", Unit = "kg", Price = 2.29m },
                new ProjectApp.Api.Models.Product { Id = 4, Sku = "SKU-004", Name = "Milk 1L", Unit = "ltr", Price = 1.19m },
                new ProjectApp.Api.Models.Product { Id = 5, Sku = "SKU-005", Name = "Butter 200g", Unit = "pkg", Price = 3.79m },
                new ProjectApp.Api.Models.Product { Id = 6, Sku = "SKU-006", Name = "Bread Loaf", Unit = "pc", Price = 1.99m },
                new ProjectApp.Api.Models.Product { Id = 7, Sku = "SKU-007", Name = "Eggs (12)", Unit = "box", Price = 2.99m },
                new ProjectApp.Api.Models.Product { Id = 8, Sku = "SKU-008", Name = "Olive Oil 500ml", Unit = "btl", Price = 6.49m },
                new ProjectApp.Api.Models.Product { Id = 9, Sku = "SKU-009", Name = "Pasta 1kg", Unit = "kg", Price = 2.59m },
                new ProjectApp.Api.Models.Product { Id = 10, Sku = "SKU-010", Name = "Tomato Sauce 300g", Unit = "jar", Price = 2.39m }
            });
            db.SaveChanges();
        }
    }
}

app.Run();

// Expose Program class for test host discovery
public partial class Program { }
