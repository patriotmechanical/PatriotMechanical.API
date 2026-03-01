using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PatriotMechanical.API.Domain.Entities;
using PatriotMechanical.API.Infrastructure.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PatriotMechanical.API.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    // ─── SETUP: First-time company + admin creation ───────────────
    [HttpPost("setup")]
    public async Task<IActionResult> Setup([FromBody] SetupRequest request)
    {
        // Only allow setup if no company exists yet
        var existingCompany = await _context.CompanySettings.AnyAsync();
        if (existingCompany)
            return BadRequest("Setup has already been completed.");

        if (string.IsNullOrWhiteSpace(request.CompanyName))
            return BadRequest("Company name is required.");

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("Email and password are required.");

        if (request.Password.Length < 8)
            return BadRequest("Password must be at least 8 characters.");

        // Create company
        var company = new CompanySettings
        {
            CompanyName = request.CompanyName.Trim(),
            ServiceTitanTenantId = request.ServiceTitanTenantId?.Trim(),
            ServiceTitanClientId = request.ServiceTitanClientId?.Trim(),
            ServiceTitanClientSecret = request.ServiceTitanClientSecret?.Trim(),
            ServiceTitanAppKey = request.ServiceTitanAppKey?.Trim()
        };

        _context.CompanySettings.Add(company);

        // Create admin user
        var user = new User
        {
            Email = request.Email.Trim().ToLower(),
            FullName = request.FullName?.Trim() ?? "",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CompanySettingsId = company.Id
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = GenerateJwt(user);

        return Ok(new
        {
            token,
            user = new { user.Id, user.Email, user.FullName },
            company = new { company.Id, company.CompanyName, company.IsServiceTitanConfigured }
        });
    }

    // ─── CHECK: Is setup needed? ──────────────────────────────────
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var company = await _context.CompanySettings.FirstOrDefaultAsync();

        return Ok(new
        {
            setupComplete = company != null,
            companyName = company?.CompanyName
        });
    }

    // ─── LOGIN ────────────────────────────────────────────────────
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _context.Users
            .Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLower());

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        if (!user.IsActive)
            return Unauthorized(new { message = "Account is deactivated." });

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var token = GenerateJwt(user);

        return Ok(new
        {
            token,
            user = new { user.Id, user.Email, user.FullName },
            company = new
            {
                user.Company.Id,
                user.Company.CompanyName,
                user.Company.IsServiceTitanConfigured
            }
        });
    }

    // ─── ME: Get current user info ────────────────────────────────
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var user = await _context.Users
            .Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound();

        return Ok(new
        {
            user = new { user.Id, user.Email, user.FullName },
            company = new
            {
                user.Company.Id,
                user.Company.CompanyName,
                user.Company.IsServiceTitanConfigured,
                user.Company.AutoSyncEnabled,
                user.Company.SyncIntervalMinutes,
                user.Company.LastSyncAt,
                user.Company.LastSyncStatus
            }
        });
    }

    // ─── CHANGE PASSWORD ──────────────────────────────────────────
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = await _context.Users.FindAsync(userId);

        if (user == null)
            return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Current password is incorrect." });

        if (request.NewPassword.Length < 8)
            return BadRequest(new { message = "New password must be at least 8 characters." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Password updated." });
    }

    // ─── JWT GENERATION ───────────────────────────────────────────
    private string GenerateJwt(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("companyId", user.CompanySettingsId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────
public class SetupRequest
{
    public string CompanyName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? FullName { get; set; }
    public string? ServiceTitanTenantId { get; set; }
    public string? ServiceTitanClientId { get; set; }
    public string? ServiceTitanClientSecret { get; set; }
    public string? ServiceTitanAppKey { get; set; }
}

public class LoginRequest
{
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = null!;
    public string NewPassword { get; set; } = null!;
}