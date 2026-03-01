using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Domain.Entities;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Controllers;

[ApiController]
[Route("migrate")]
public class MigrateController : ControllerBase
{
    private readonly AppDbContext _context;

    public MigrateController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Hit GET /migrate/fix ONE TIME to seed CompanySettings and link existing users.
    /// DELETE THIS CONTROLLER after it works.
    /// </summary>
    [HttpGet("fix")]
    public async Task<IActionResult> Fix()
    {
        // Check if CompanySettings already exists
        var existing = await _context.CompanySettings.FirstOrDefaultAsync();
        if (existing != null)
            return Ok(new { message = "CompanySettings already exists. No action needed.", existing.CompanyName });

        // Step 1: Create CompanySettings with your ServiceTitan creds
        var company = new CompanySettings
        {
            Id = Guid.NewGuid(),
            CompanyName = "Patriot Mechanical",
            ServiceTitanTenantId = "4146821403",
            ServiceTitanClientId = "cid.li8qk0ipffsz3386crc674xzz",
            ServiceTitanClientSecret = "cs3.95u51txzvi0jdmb956jgicf63fcmi9tc2g958ucmj0zgfpq9rf",
            ServiceTitanAppKey = "ak1.9agbq4a1t6198nhsaveokkn28",
            AutoSyncEnabled = true,
            SyncIntervalMinutes = 60,
            CreditCardFeePercent = 2.5m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.CompanySettings.Add(company);
        await _context.SaveChangesAsync();

        // Step 2: Point all existing users to this company
        var users = await _context.Users.ToListAsync();
        foreach (var user in users)
        {
            user.CompanySettingsId = company.Id;
        }
        await _context.SaveChangesAsync();

        // Step 3: Add the FK constraint and mark migration complete via raw SQL
        try
        {
            // Check if FK already exists
            var fkExists = await _context.Database.ExecuteSqlRawAsync(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.table_constraints 
                        WHERE constraint_name = 'FK_Users_CompanySettings_CompanySettingsId'
                    ) THEN
                        ALTER TABLE ""Users"" ADD CONSTRAINT ""FK_Users_CompanySettings_CompanySettingsId"" 
                        FOREIGN KEY (""CompanySettingsId"") REFERENCES ""CompanySettings"" (""Id"") ON DELETE CASCADE;
                    END IF;
                END $$;
            ");

            // Mark migration as applied
            await _context.Database.ExecuteSqlRawAsync(@"
                INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                SELECT '20260301024809_AddCompanySettingsAndUserUpgrade', '10.0.0'
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""__EFMigrationsHistory"" 
                    WHERE ""MigrationId"" = '20260301024809_AddCompanySettingsAndUserUpgrade'
                );
            ");
        }
        catch (Exception ex)
        {
            return Ok(new { 
                message = "CompanySettings created and users linked. FK/migration step had an issue but may already be done.", 
                error = ex.Message,
                companyId = company.Id,
                usersUpdated = users.Count 
            });
        }

        return Ok(new
        {
            message = "Migration fix complete!",
            companyId = company.Id,
            companyName = company.CompanyName,
            usersUpdated = users.Count
        });
    }
}