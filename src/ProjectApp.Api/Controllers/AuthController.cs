using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProjectApp.Api.Auth;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IJwtTokenService tokenService, IOptions<JwtSettings> jwtOptions, IConfiguration config) : ControllerBase
{
    public record LoginRequest(string UserName, string Password);
    public record LoginResponse(string AccessToken, string Role, DateTime ExpiresAtUtc);
    public class ConfigUser { public string UserName { get; set; } = string.Empty; public string Password { get; set; } = string.Empty; public string Role { get; set; } = "Manager"; }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.UserName) || string.IsNullOrWhiteSpace(req.Password))
            return Unauthorized();

        var users = config.GetSection("Users").Get<List<ConfigUser>>() ?? new List<ConfigUser>
        {
            new() { UserName = "admin", Password = "140606tl", Role = "Admin" },
            new() { UserName = "manager", Password = "140606tl", Role = "Manager" },
        };

        var user = users.FirstOrDefault(u => string.Equals(u.UserName, req.UserName, StringComparison.OrdinalIgnoreCase)
                                           && u.Password == req.Password);
        if (user is null) return Unauthorized();

        var token = tokenService.CreateToken(user.UserName, user.UserName, user.Role);
        var exp = DateTime.UtcNow.AddMinutes(jwtOptions.Value.AccessTokenLifetimeMinutes);
        return Ok(new LoginResponse(token, user.Role, exp));
    }
}
