using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectApp.Api.Data;
using ProjectApp.Api.Models;
using ProjectApp.Api.Services;

namespace ProjectApp.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "AdminOnly")] // только администраторы управляют пользователями
public class UsersController(AppDbContext db, IPasswordHasher hasher) : ControllerBase
{
    public record UserDto(int Id, string UserName, string DisplayName, string Role, bool IsActive, DateTime CreatedAt);
    public record CreateUserRequest(string UserName, string DisplayName, string Role, string? Password);
    public record UpdateRoleRequest(string Role);
    public record UpdateStatusRequest(bool IsActive);
    public record ResetPasswordRequest(string NewPassword);

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll(CancellationToken ct)
    {
        var users = await db.Users
            .AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(u => new UserDto(u.Id, u.UserName, u.DisplayName, u.Role, u.IsActive, u.CreatedAt))
            .ToListAsync(ct);
        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.UserName) || string.IsNullOrWhiteSpace(req.Role))
            return BadRequest("UserName and Role are required");
        var userName = req.UserName.Trim().ToLower();
        var role = string.Equals(req.Role?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "Manager";
        var exists = await db.Users.AnyAsync(u => u.UserName.ToLower() == userName, ct);
        if (exists) return Conflict("UserName already exists");
        var u = new User
        {
            UserName = userName,
            DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? userName : req.DisplayName.Trim(),
            Role = role,
            IsPasswordless = !string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase),
            PasswordHash = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
                ? (string.IsNullOrWhiteSpace(req.Password) ? null : hasher.Hash(req.Password!)) ?? string.Empty
                : string.Empty,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        if (u.Role == "Admin" && string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("Password is required for Admin users");
        db.Users.Add(u);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetAll), new { id = u.Id }, new UserDto(u.Id, u.UserName, u.DisplayName, u.Role, u.IsActive, u.CreatedAt));
    }

    [HttpPut("{id:int}/role")]
    public async Task<IActionResult> SetRole([FromRoute] int id, [FromBody] UpdateRoleRequest req, CancellationToken ct)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return NotFound();
        u.Role = req.Role;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> SetStatus([FromRoute] int id, [FromBody] UpdateStatusRequest req, CancellationToken ct)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return NotFound();
        u.IsActive = req.IsActive;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("{id:int}/password")]
    public async Task<IActionResult> ResetPassword([FromRoute] int id, [FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.NewPassword)) return BadRequest("NewPassword is required");
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return NotFound();
        u.PasswordHash = hasher.Hash(req.NewPassword);
        u.IsPasswordless = false; // once password set, require it
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken ct)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return NotFound();
        db.Users.Remove(u);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
