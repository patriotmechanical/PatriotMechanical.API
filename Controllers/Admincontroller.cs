using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Infrastructure.Data;
using System.Security.Claims;

namespace PatriotMechanical.API.Controllers;

[ApiController]
[Route("admin")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminController(AppDbContext context)
    {
        _context = context;
    }

    // ─── GET COMPANY SETTINGS ─────────────────────────────────────
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings()
    {
        var companyId = GetCompanyId();
        var company = await _context.CompanySettings.FindAsync(companyId);

        if (company == null)
            return NotFound();

        return Ok(new
        {
            company.Id,
            company.CompanyName,
            company.AutoSyncEnabled,
            company.SyncIntervalMinutes,
            company.CreditCardFeePercent,
            company.LastSyncAt,
            company.LastSyncStatus,
            company.CreatedAt,
            serviceTitan = new
            {
                isConfigured = company.IsServiceTitanConfigured,
                tenantId = company.ServiceTitanTenantId ?? "",
                // Only show last 4 chars of secrets for security
                clientId = MaskSecret(company.ServiceTitanClientId),
                clientSecret = MaskSecret(company.ServiceTitanClientSecret),
                appKey = MaskSecret(company.ServiceTitanAppKey)
            }
        });
    }

    // ─── UPDATE COMPANY INFO ──────────────────────────────────────
    [HttpPut("settings/company")]
    public async Task<IActionResult> UpdateCompany([FromBody] UpdateCompanyRequest request)
    {
        var companyId = GetCompanyId();
        var company = await _context.CompanySettings.FindAsync(companyId);

        if (company == null)
            return NotFound();

        if (!string.IsNullOrWhiteSpace(request.CompanyName))
            company.CompanyName = request.CompanyName.Trim();

        if (request.CreditCardFeePercent.HasValue)
            company.CreditCardFeePercent = request.CreditCardFeePercent.Value;

        company.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Company settings updated." });
    }

    // ─── UPDATE SERVICETITAN CREDENTIALS ──────────────────────────
    [HttpPut("settings/servicetitan")]
    public async Task<IActionResult> UpdateServiceTitan([FromBody] UpdateServiceTitanRequest request)
    {
        var companyId = GetCompanyId();
        var company = await _context.CompanySettings.FindAsync(companyId);

        if (company == null)
            return NotFound();

        company.ServiceTitanTenantId = request.TenantId?.Trim();
        company.ServiceTitanClientId = request.ClientId?.Trim();
        company.ServiceTitanClientSecret = request.ClientSecret?.Trim();
        company.ServiceTitanAppKey = request.AppKey?.Trim();
        company.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "ServiceTitan credentials updated.",
            isConfigured = company.IsServiceTitanConfigured
        });
    }

    // ─── TEST SERVICETITAN CONNECTION ─────────────────────────────
    [HttpPost("settings/servicetitan/test")]
    public async Task<IActionResult> TestServiceTitanConnection()
    {
        var companyId = GetCompanyId();
        var company = await _context.CompanySettings.FindAsync(companyId);

        if (company == null || !company.IsServiceTitanConfigured)
            return BadRequest(new { message = "ServiceTitan credentials are not configured." });

        try
        {
            using var httpClient = new HttpClient();

            var tokenRequest = new HttpRequestMessage(HttpMethod.Post,
                "https://auth.servicetitan.io/connect/token");

            tokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", company.ServiceTitanClientId! },
                { "client_secret", company.ServiceTitanClientSecret! }
            });

            var response = await httpClient.SendAsync(tokenRequest);

            if (response.IsSuccessStatusCode)
            {
                return Ok(new { success = true, message = "Connection successful!" });
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return Ok(new { success = false, message = $"Authentication failed: {response.StatusCode}" });
            }
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = $"Connection error: {ex.Message}" });
        }
    }

    // ─── UPDATE SYNC SETTINGS ─────────────────────────────────────
    [HttpPut("settings/sync")]
    public async Task<IActionResult> UpdateSyncSettings([FromBody] UpdateSyncRequest request)
    {
        var companyId = GetCompanyId();
        var company = await _context.CompanySettings.FindAsync(companyId);

        if (company == null)
            return NotFound();

        company.AutoSyncEnabled = request.AutoSyncEnabled;

        if (request.SyncIntervalMinutes >= 15 && request.SyncIntervalMinutes <= 1440)
            company.SyncIntervalMinutes = request.SyncIntervalMinutes;

        company.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Sync settings updated." });
    }

    // ═══════════════════════════════════════════════════════════════
    // USER MANAGEMENT
    // ═══════════════════════════════════════════════════════════════

    // ─── LIST ALL USERS ───────────────────────────────────────────
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var companyId = GetCompanyId();

        var users = await _context.Users
            .Where(u => u.CompanySettingsId == companyId)
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.FullName,
                u.IsActive,
                u.CreatedAt,
                u.LastLoginAt
            })
            .ToListAsync();

        return Ok(users);
    }

    // ─── CREATE NEW USER ──────────────────────────────────────────
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        var companyId = GetCompanyId();

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Email is required." });

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return BadRequest(new { message = "Password must be at least 8 characters." });

        var email = request.Email.Trim().ToLower();

        // Check for duplicate email
        var exists = await _context.Users.AnyAsync(u => u.Email == email);
        if (exists)
            return BadRequest(new { message = "A user with this email already exists." });

        var user = new PatriotMechanical.API.Domain.Entities.User
        {
            Email = email,
            FullName = request.FullName?.Trim() ?? "",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CompanySettingsId = companyId,
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            user.Id,
            user.Email,
            user.FullName,
            user.IsActive,
            user.CreatedAt,
            message = "User created."
        });
    }

    // ─── UPDATE USER ──────────────────────────────────────────────
    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var companyId = GetCompanyId();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.CompanySettingsId == companyId);

        if (user == null)
            return NotFound(new { message = "User not found." });

        if (!string.IsNullOrWhiteSpace(request.FullName))
            user.FullName = request.FullName.Trim();

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var newEmail = request.Email.Trim().ToLower();
            var emailTaken = await _context.Users
                .AnyAsync(u => u.Email == newEmail && u.Id != id);

            if (emailTaken)
                return BadRequest(new { message = "Email is already in use." });

            user.Email = newEmail;
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = "User updated." });
    }

    // ─── RESET USER PASSWORD ──────────────────────────────────────
    [HttpPut("users/{id}/reset-password")]
    public async Task<IActionResult> ResetUserPassword(Guid id, [FromBody] ResetPasswordRequest request)
    {
        var companyId = GetCompanyId();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.CompanySettingsId == companyId);

        if (user == null)
            return NotFound(new { message = "User not found." });

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            return BadRequest(new { message = "Password must be at least 8 characters." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Password reset." });
    }

    // ─── TOGGLE USER ACTIVE/INACTIVE ─────────────────────────────
    [HttpPut("users/{id}/toggle-active")]
    public async Task<IActionResult> ToggleUserActive(Guid id)
    {
        var companyId = GetCompanyId();
        var currentUserId = GetCurrentUserId();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.CompanySettingsId == companyId);

        if (user == null)
            return NotFound(new { message = "User not found." });

        // Prevent deactivating yourself
        if (user.Id == currentUserId)
            return BadRequest(new { message = "You can't deactivate your own account." });

        user.IsActive = !user.IsActive;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = user.IsActive ? "User reactivated." : "User deactivated.",
            isActive = user.IsActive
        });
    }

    // ─── DELETE USER ──────────────────────────────────────────────
    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var companyId = GetCompanyId();
        var currentUserId = GetCurrentUserId();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id && u.CompanySettingsId == companyId);

        if (user == null)
            return NotFound(new { message = "User not found." });

        if (user.Id == currentUserId)
            return BadRequest(new { message = "You can't delete your own account." });

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return Ok(new { message = "User deleted." });
    }

    // ─── HELPERS ──────────────────────────────────────────────────
    private Guid GetCompanyId()
    {
        var claim = User.FindFirst("companyId")?.Value;
        return Guid.Parse(claim!);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.Parse(claim!);
    }

    private static string MaskSecret(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Length <= 6) return "••••••";
        return "••••••" + value[^4..];
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────
public class UpdateCompanyRequest
{
    public string? CompanyName { get; set; }
    public decimal? CreditCardFeePercent { get; set; }
}

public class UpdateServiceTitanRequest
{
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? AppKey { get; set; }
}

public class UpdateSyncRequest
{
    public bool AutoSyncEnabled { get; set; }
    public int SyncIntervalMinutes { get; set; }
}

public class CreateUserRequest
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? FullName { get; set; }
}

public class UpdateUserRequest
{
    public string? Email { get; set; }
    public string? FullName { get; set; }
}

public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = null!;
}