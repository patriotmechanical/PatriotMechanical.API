using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PatriotMechanical.API.Application.Services;
using PatriotMechanical.API.Infrastructure.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PatriotMechanical.API.Controllers
{
    [ApiController]
    [Route("demo")]
    public class DemoController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public DemoController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // POST /demo/login — auto-login as demo user
        [HttpPost("login")]
        public async Task<IActionResult> DemoLogin()
        {
            // Ensure demo data exists
            var demoUser = await _context.Users
                .Include(u => u.Company)
                .FirstOrDefaultAsync(u => u.Id == DemoSeeder.DemoUserId);

            if (demoUser == null)
            {
                // Seed demo data
                await DemoSeeder.ResetDemoDataAsync(_context);
                demoUser = await _context.Users
                    .Include(u => u.Company)
                    .FirstOrDefaultAsync(u => u.Id == DemoSeeder.DemoUserId);
            }

            if (demoUser == null)
                return StatusCode(500, new { message = "Failed to create demo account." });

            demoUser.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var token = GenerateJwt(demoUser);

            return Ok(new
            {
                token,
                user = new { demoUser.Id, demoUser.Email, demoUser.FullName },
                company = new
                {
                    demoUser.Company.Id,
                    demoUser.Company.CompanyName,
                    demoUser.Company.IsServiceTitanConfigured
                },
                isDemo = true
            });
        }

        private string GenerateJwt(PatriotMechanical.API.Domain.Entities.User user)
        {
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim("companyId", user.CompanySettingsId.ToString()),
                new Claim("isDemo", "true")
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(24),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}