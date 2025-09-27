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
    public record LoginRequest(string UserName, string Password);
    public record LoginResponse(string AccessToken, string Role, DateTime ExpiresAtUtc, string UserName, string DisplayName);
    public class ConfigUser { public string UserName { get; set; } = string.Empty; public string Password { get; set; } = string.Empty; public string Role { get; set; } = "Manager"; }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.UserName))
            return Unauthorized();

        // 1) Try database-backed users first
        var dbUser = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName.ToLower() == req.UserName.Trim().ToLower());
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

        // 2) Fallback to config users (for local/dev)
        var users = config.GetSection("Users").Get<List<ConfigUser>>() ?? new List<ConfigUser>
        {
            new() { UserName = "admin", Password = "140606tl", Role = "Admin" },
            new() { UserName = "manager", Password = "140606tl", Role = "Manager" },
        };

        var cfgUser = users.FirstOrDefault(u => string.Equals(u.UserName, req.UserName, StringComparison.OrdinalIgnoreCase)
                                           && u.Password == req.Password);
        if (cfgUser is null) return Unauthorized();

        var token = tokenService.CreateToken(cfgUser.UserName, cfgUser.UserName, cfgUser.Role);
        var exp = DateTime.UtcNow.AddMinutes(jwtOptions.Value.AccessTokenLifetimeMinutes);
        return Ok(new LoginResponse(token, cfgUser.Role, exp, cfgUser.UserName, cfgUser.UserName));
    }
}
