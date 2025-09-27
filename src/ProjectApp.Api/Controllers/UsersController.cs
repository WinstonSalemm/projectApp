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
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Try to self-heal if IsPasswordless column is missing
            if (IsMissingColumnError(ex, "IsPasswordless"))
            {
                await EnsureUsersIsPasswordlessColumnAsync(ct);
                await db.SaveChangesAsync(ct);
            }
            else
            {
                throw;
            }
        }
        return CreatedAtAction(nameof(GetAll), new { id = u.Id }, new UserDto(u.Id, u.UserName, u.DisplayName, u.Role, u.IsActive, u.CreatedAt));
    }

    [HttpPut("{id:int}/role")]
    public async Task<IActionResult> SetRole([FromRoute] int id, [FromBody] UpdateRoleRequest req, CancellationToken ct)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return NotFound();
        u.Role = req.Role;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            if (IsMissingColumnError(ex, "IsPasswordless"))
            {
                await EnsureUsersIsPasswordlessColumnAsync(ct);
                await db.SaveChangesAsync(ct);
            }
            else throw;
        }
        return NoContent();
    }

    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> SetStatus([FromRoute] int id, [FromBody] UpdateStatusRequest req, CancellationToken ct)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return NotFound();
        u.IsActive = req.IsActive;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            if (IsMissingColumnError(ex, "IsPasswordless"))
            {
                await EnsureUsersIsPasswordlessColumnAsync(ct);
                await db.SaveChangesAsync(ct);
            }
            else throw;
        }
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
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            if (IsMissingColumnError(ex, "IsPasswordless"))
            {
                await EnsureUsersIsPasswordlessColumnAsync(ct);
                await db.SaveChangesAsync(ct);
            }
            else throw;
        }
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

    private static bool IsMissingColumnError(Exception ex, string column)
    {
        var msg = ex.ToString();
        return msg.Contains(column, StringComparison.OrdinalIgnoreCase)
            && (msg.Contains("no such column", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("Unknown column", StringComparison.OrdinalIgnoreCase));
    }

    private async Task EnsureUsersIsPasswordlessColumnAsync(CancellationToken ct)
    {
        var provider = db.Database.ProviderName ?? string.Empty;
        if (provider.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE `Users` ADD COLUMN IF NOT EXISTS `IsPasswordless` TINYINT(1) NOT NULL DEFAULT 0;", ct);
        }
        else if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var hasCol = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync(ct);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('Users');";
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var name = reader.GetString(1);
                    if (string.Equals(name, "IsPasswordless", StringComparison.OrdinalIgnoreCase)) { hasCol = true; break; }
                }
            }
            if (!hasCol)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Users ADD COLUMN IsPasswordless INTEGER NOT NULL DEFAULT 0;", ct);
            }
        }
    }
}
