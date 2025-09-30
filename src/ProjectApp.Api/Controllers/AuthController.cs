using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProjectApp.Api.Auth;
using ProjectApp.Api.Data;
using ProjectApp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IJwtTokenService tokenService, IOptions<JwtSettings> jwtOptions, IConfiguration config, AppDbContext db, IPasswordHasher hasher) : ControllerBase
{
    public record LoginRequest(string UserName, string? Password);
    public record LoginResponse(string AccessToken, string Role, DateTime ExpiresAtUtc, string UserName, string DisplayName);
    public class ConfigUser { public string UserName { get; set; } = string.Empty; public string Password { get; set; } = string.Empty; public string Role { get; set; } = "Manager"; public string DisplayName { get; set; } = string.Empty; }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.UserName))
            return Unauthorized();

        // 1) Try database-backed users first (resilient to schema mismatches)
        Models.User? dbUser = null;
        try
        {
            dbUser = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserName.ToLower() == req.UserName.Trim().ToLower());
        }
        catch
        {
            dbUser = null; // fall back to config users
        }
        if (dbUser is not null && dbUser.IsActive)
        {
            var isAdmin = string.Equals(dbUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);
            if (!dbUser.IsPasswordless || isAdmin)
            {
                // Password required path (admins and non-passwordless users)
                if (!string.IsNullOrEmpty(req.Password) && hasher.Verify(req.Password, dbUser.PasswordHash))
                {
                    var token1 = tokenService.CreateToken(dbUser.Id.ToString(), dbUser.UserName, dbUser.Role);
                    var exp1 = DateTime.UtcNow.AddMinutes(jwtOptions.Value.AccessTokenLifetimeMinutes);
                    return Ok(new LoginResponse(token1, dbUser.Role, exp1, dbUser.UserName, dbUser.DisplayName));
                }
            }
            else
            {
                // Passwordless login (e.g., Manager)
                var token1 = tokenService.CreateToken(dbUser.Id.ToString(), dbUser.UserName, dbUser.Role);
                var exp1 = DateTime.UtcNow.AddMinutes(jwtOptions.Value.AccessTokenLifetimeMinutes);
                return Ok(new LoginResponse(token1, dbUser.Role, exp1, dbUser.UserName, dbUser.DisplayName));
            }
        }

        // 2) Fallback to config users (for local/dev and bootstrap). If section is missing or empty, use built-ins.
        var configured = config.GetSection("Users").Get<List<ConfigUser>>();
        var users = (configured == null || configured.Count == 0)
            ? new List<ConfigUser>
            {
                new() { UserName = "admin",   Password = "140606tl", Role = "Admin",   DisplayName = "Администратор" },
                // Passwordless managers (accept null/empty password)
                new() { UserName = "shop",    Password = "",         Role = "Manager", DisplayName = "Магазин" },
                new() { UserName = "liliya",  Password = "",         Role = "Manager", DisplayName = "Лилия" },
                new() { UserName = "timur",   Password = "",         Role = "Manager", DisplayName = "Тимур" },
                new() { UserName = "valeriy", Password = "",         Role = "Manager", DisplayName = "Валерий" },
                new() { UserName = "albert",  Password = "",         Role = "Manager", DisplayName = "Альберт" },
                new() { UserName = "rasim",   Password = "",         Role = "Manager", DisplayName = "Расим" },
                new() { UserName = "alisher", Password = "",         Role = "Manager", DisplayName = "Алишер" },
            }
            : configured;

        // Ensure built-ins exist even if section present
        void Ensure(List<ConfigUser> list, string u, string role, string display, string? pwd = "")
        {
            if (!list.Any(x => string.Equals(x.UserName, u, StringComparison.OrdinalIgnoreCase)))
                list.Add(new ConfigUser { UserName = u, Password = pwd ?? string.Empty, Role = role, DisplayName = display });
        }
        Ensure(users, "admin",   "Admin",   "Администратор", "140606tl");
        Ensure(users, "shop",    "Manager", "Магазин",      "");
        Ensure(users, "liliya",  "Manager", "Лилия",        "");
        Ensure(users, "timur",   "Manager", "Тимур",         "");
        Ensure(users, "valeriy", "Manager", "Валерий",       "");
        Ensure(users, "albert",  "Manager", "Альберт",       "");
        Ensure(users, "rasim",   "Manager", "Расим",         "");
        Ensure(users, "alisher", "Manager", "Алишер",        "");

        var reqPwd = req.Password ?? string.Empty; // treat null as empty for passwordless logins
        var cfgUser = users.FirstOrDefault(u => string.Equals(u.UserName, req.UserName, StringComparison.OrdinalIgnoreCase)
                                           && string.Equals((u.Password ?? string.Empty), reqPwd, StringComparison.Ordinal));
        if (cfgUser is null) return Unauthorized();

        var token = tokenService.CreateToken(cfgUser.UserName, cfgUser.UserName, cfgUser.Role);
        var exp = DateTime.UtcNow.AddMinutes(jwtOptions.Value.AccessTokenLifetimeMinutes);
        var display = string.IsNullOrWhiteSpace(cfgUser.DisplayName) ? cfgUser.UserName : cfgUser.DisplayName;
        return Ok(new LoginResponse(token, cfgUser.Role, exp, cfgUser.UserName, display));
    }
}
