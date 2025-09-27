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

    private async Task<bool> RawUserExistsByUserNameAsync(string userName, CancellationToken ct)
    {
        var provider = db.Database.ProviderName ?? string.Empty;
        var table = await ResolveActualUsersTableAsync(ct);
        using var conn = db.Database.GetDbConnection();
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        if (provider.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            cmd.CommandText = $"SELECT 1 FROM `{table}` WHERE UserName = @p0 LIMIT 1";
        }
        else
        {
            cmd.CommandText = $"SELECT 1 FROM {table} WHERE UserName = @p0 LIMIT 1";
        }
        var p = cmd.CreateParameter(); p.ParameterName = "@p0"; p.Value = userName; cmd.Parameters.Add(p);
        var obj = await cmd.ExecuteScalarAsync(ct);
        return obj != null && obj != DBNull.Value;
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> GetById([FromRoute] int id, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return NotFound();
        return Ok(new UserDto(u.Id, u.UserName, u.DisplayName, u.Role, u.IsActive, u.CreatedAt));
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.UserName) || string.IsNullOrWhiteSpace(req.Role))
            return BadRequest("UserName and Role are required");
        var userName = req.UserName.Trim().ToLower();
        var role = string.Equals(req.Role?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "Manager";
        bool exists;
        try
        {
            exists = await db.Users.AnyAsync(u => u.UserName.ToLower() == userName, ct);
        }
        catch (Exception ex)
        {
            if (IsMissingColumnError(ex, "IsPasswordless") || IsMissingTableError(ex, "Users"))
            {
                exists = await RawUserExistsByUserNameAsync(userName, ct);
            }
            else throw;
        }
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
            return CreatedAtAction(nameof(GetById), new { id = u.Id }, new UserDto(u.Id, u.UserName, u.DisplayName, u.Role, u.IsActive, u.CreatedAt));
        }
        catch (Exception ex)
        {
            // Try to self-heal if IsPasswordless column is missing
            if (IsMissingColumnError(ex, "IsPasswordless"))
            {
                try
                {
                    await EnsureUsersIsPasswordlessColumnAsync(ct);
                    await db.SaveChangesAsync(ct);
                    return CreatedAtAction(nameof(GetById), new { id = u.Id }, new UserDto(u.Id, u.UserName, u.DisplayName, u.Role, u.IsActive, u.CreatedAt));
                }
                catch (Exception ex2)
                {
                    // If ALTER TABLE is not permitted, fallback to manual INSERT without IsPasswordless
                    try
                    {
                        await InsertUserWithoutIsPasswordlessAsync(u, ct);
                        var id = await RawGetUserIdByUserNameAsync(u.UserName, ct);
                        var dto = new UserDto(id ?? 0, u.UserName, u.DisplayName, u.Role, u.IsActive, u.CreatedAt);
                        var location = id.HasValue ? $"/api/users/{id.Value}" : "/api/users";
                        return Created(location, dto);
                    }
                    catch (Exception insertEx)
                    {
                        if (IsDuplicateKeyError(insertEx)) return Conflict("UserName already exists");
                        throw;
                    }
                }
            }
            if (IsDuplicateKeyError(ex)) return Conflict("UserName already exists");
            throw;
        }
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

    private static bool IsDuplicateKeyError(Exception ex)
    {
        var msg = ex.ToString();
        return msg.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMissingTableError(Exception ex, string table)
    {
        var msg = ex.ToString();
        return msg.Contains(table, StringComparison.OrdinalIgnoreCase)
            && (msg.Contains("no such table", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase)
                || msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
    }

    private async Task EnsureUsersIsPasswordlessColumnAsync(CancellationToken ct)
    {
        var provider = db.Database.ProviderName ?? string.Empty;
        if (provider.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            string actualTable = "Users";
            bool hasCol = false;
            using (var conn = db.Database.GetDbConnection())
            {
                await conn.OpenAsync(ct);
                // Resolve actual table name (case-insensitive)
                await using (var cmdTbl = conn.CreateCommand())
                {
                    cmdTbl.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND UPPER(TABLE_NAME) = 'USERS' LIMIT 1";
                    var obj = await cmdTbl.ExecuteScalarAsync(ct);
                    if (obj is string s && !string.IsNullOrWhiteSpace(s)) actualTable = s;
                }
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND UPPER(TABLE_NAME) = 'USERS' AND UPPER(COLUMN_NAME) = 'ISPASSWORDLESS'";
                var scalar = await cmd.ExecuteScalarAsync(ct);
                hasCol = scalar != null && scalar != DBNull.Value && Convert.ToInt64(scalar) > 0;
            }
            if (!hasCol)
            {
                await db.Database.ExecuteSqlRawAsync($"ALTER TABLE `{actualTable}` ADD COLUMN `IsPasswordless` TINYINT(1) NOT NULL DEFAULT 0;", ct);
            }
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

    private async Task<string> ResolveActualUsersTableAsync(CancellationToken ct)
    {
        var provider = db.Database.ProviderName ?? string.Empty;
        if (provider.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            using var conn = db.Database.GetDbConnection();
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND UPPER(TABLE_NAME) = 'USERS' LIMIT 1";
            var obj = await cmd.ExecuteScalarAsync(ct);
            return obj is string s && !string.IsNullOrWhiteSpace(s) ? s : "Users";
        }
        return "Users";
    }

    private async Task InsertUserWithoutIsPasswordlessAsync(User u, CancellationToken ct)
    {
        var provider = db.Database.ProviderName ?? string.Empty;
        var table = await ResolveActualUsersTableAsync(ct);
        if (provider.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            var sql = $"INSERT INTO `{table}` (`UserName`,`DisplayName`,`Role`,`PasswordHash`,`IsActive`,`CreatedAt`) VALUES (@p0,@p1,@p2,@p3,@p4,@p5);";
            await db.Database.ExecuteSqlRawAsync(sql, new object[] { u.UserName, u.DisplayName, u.Role, u.PasswordHash, u.IsActive ? 1 : 0, u.CreatedAt }, ct);
        }
        else if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var sql = $"INSERT INTO {table} (UserName,DisplayName,Role,PasswordHash,IsActive,CreatedAt) VALUES (@p0,@p1,@p2,@p3,@p4,@p5);";
            await db.Database.ExecuteSqlRawAsync(sql, new object[] { u.UserName, u.DisplayName, u.Role, u.PasswordHash, u.IsActive ? 1 : 0, u.CreatedAt.ToString("o") }, ct);
        }
    }

    private async Task<int?> RawGetUserIdByUserNameAsync(string userName, CancellationToken ct)
    {
        var provider = db.Database.ProviderName ?? string.Empty;
        var table = await ResolveActualUsersTableAsync(ct);
        using var conn = db.Database.GetDbConnection();
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        if (provider.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            cmd.CommandText = $"SELECT Id FROM `{table}` WHERE UserName = @p0 LIMIT 1";
        }
        else
        {
            cmd.CommandText = $"SELECT Id FROM {table} WHERE UserName = @p0 LIMIT 1";
        }
        var p = cmd.CreateParameter(); p.ParameterName = "@p0"; p.Value = userName; cmd.Parameters.Add(p);
        var obj = await cmd.ExecuteScalarAsync(ct);
        return obj == null || obj == DBNull.Value ? (int?)null : Convert.ToInt32(obj);
    }
}
