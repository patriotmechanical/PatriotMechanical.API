using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Domain.Entities;
using PatriotMechanical.API.Infrastructure.Data;
using PatriotMechanical.API.Application.Services;
using System.Text.Json;

namespace PatriotMechanical.API.Controllers;

[ApiController]
[Route("migrate")]
public class MigrateController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ServiceTitanService _stService;
    private readonly ServiceTitanSyncEngine _syncEngine;

    public MigrateController(AppDbContext context, ServiceTitanService stService, ServiceTitanSyncEngine syncEngine)
    {
        _context = context;
        _stService = stService;
        _syncEngine = syncEngine;
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

    /// <summary>
    /// Hit GET /migrate/backfill-invoice-dates ONE TIME to fix IssueDate on invoices 
    /// that were synced before date parsing was added (they have DateTime.MinValue).
    /// </summary>
    [HttpGet("backfill-invoice-dates")]
    public async Task<IActionResult> BackfillInvoiceDates()
    {
        int updated = 0, skipped = 0, failed = 0;
        int page = 1;
        bool hasMore;

        do
        {
            string raw;
            try { raw = await _stService.GetInvoicesPageAsync(page); }
            catch { break; }

            var parsed = JsonSerializer.Deserialize<JsonElement>(raw);
            hasMore = parsed.GetProperty("hasMore").GetBoolean();
            var invoices = parsed.GetProperty("data");

            foreach (var inv in invoices.EnumerateArray())
            {
                try
                {
                    var invoiceId = inv.GetProperty("id").GetInt64();

                    // Parse the date field from ST
                    if (!inv.TryGetProperty("date", out var dateProp) ||
                        dateProp.ValueKind != JsonValueKind.String ||
                        !DateTime.TryParse(dateProp.GetString(), null,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out var parsedDate))
                    {
                        skipped++;
                        continue;
                    }

                    var existing = await _context.Invoices
                        .FirstOrDefaultAsync(i => i.ServiceTitanInvoiceId == invoiceId);

                    if (existing == null) { skipped++; continue; }

                    // Only update if currently unset
                    if (existing.IssueDate == DateTime.MinValue || existing.IssueDate.Year < 2000)
                    {
                        existing.IssueDate = parsedDate;
                        existing.InvoiceDate = parsedDate;
                        updated++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch { failed++; }
            }

            await _context.SaveChangesAsync();
            page++;

        } while (hasMore);

        return Ok(new { updated, skipped, failed, message = "Invoice date backfill complete." });
    }

    /// <summary>
    /// Hit GET /migrate/apply-appointments ONE TIME to create the Appointments table.
    /// </summary>
    [HttpGet("apply-appointments")]
    public async Task<IActionResult> ApplyAppointments()
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""Appointments"" (
                    ""Id""                          uuid            NOT NULL PRIMARY KEY,
                    ""ServiceTitanAppointmentId""   bigint          NOT NULL,
                    ""ServiceTitanJobId""           bigint          NOT NULL DEFAULT 0,
                    ""WorkOrderId""                 uuid            NULL,
                    ""Start""                       timestamptz     NOT NULL,
                    ""End""                         timestamptz     NOT NULL DEFAULT '0001-01-01',
                    ""Status""                      text            NOT NULL DEFAULT 'Scheduled',
                    ""TechnicianCount""             int             NOT NULL DEFAULT 0,
                    ""LastSyncedAt""                timestamptz     NOT NULL DEFAULT now(),
                    CONSTRAINT ""FK_Appointments_WorkOrders"" FOREIGN KEY (""WorkOrderId"")
                        REFERENCES ""WorkOrders""(""Id"") ON DELETE SET NULL
                );
                CREATE INDEX IF NOT EXISTS ""IX_Appointments_Start"" ON ""Appointments""(""Start"");
                CREATE INDEX IF NOT EXISTS ""IX_Appointments_ServiceTitanAppointmentId"" ON ""Appointments""(""ServiceTitanAppointmentId"");
            ");

            return Ok(new { message = "Appointments table created (or already exists)." });
        }
        catch (Exception ex)
        {
            return Ok(new { message = "Error creating table.", error = ex.Message });
        }
    }

    /// <summary>
    /// GET /migrate/apply-appointment-technicians — create AppointmentTechnicians table
    /// </summary>
    [HttpGet("apply-appointment-technicians")]
    public async Task<IActionResult> ApplyAppointmentTechnicians()
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""AppointmentTechnicians"" (
                    ""Id""                          uuid    NOT NULL PRIMARY KEY,
                    ""AppointmentId""               uuid    NOT NULL,
                    ""ServiceTitanTechnicianId""    bigint  NOT NULL DEFAULT 0,
                    ""TechnicianName""              text    NOT NULL DEFAULT '',
                    ""ServiceTitanJobId""           bigint  NOT NULL DEFAULT 0,
                    ""ServiceTitanAppointmentId""   bigint  NOT NULL DEFAULT 0,
                    CONSTRAINT ""FK_AppointmentTechnicians_Appointments"" FOREIGN KEY (""AppointmentId"")
                        REFERENCES ""Appointments""(""Id"") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS ""IX_AppointmentTechnicians_AppointmentId""
                    ON ""AppointmentTechnicians""(""AppointmentId"");
            ");
            return Ok(new { message = "AppointmentTechnicians table created (or already exists)." });
        }
        catch (Exception ex)
        {
            return Ok(new { message = "Error.", error = ex.Message });
        }
    }

    /// <summary>
    /// GET /migrate/sync-appointments — trigger appointment sync manually for testing
    /// </summary>
    [HttpGet("sync-appointments")]
    public async Task<IActionResult> SyncAppointments()
    {
        try
        {
            await _syncEngine.SyncAppointmentsAsync();
            var count = await _context.Appointments.CountAsync();
            return Ok(new { message = "Appointment sync complete.", totalInDb = count });
        }
        catch (Exception ex)
        {
            return Ok(new { message = "Sync failed.", error = ex.Message });
        }
    }

    /// <summary>
    /// GET /migrate/debug-appointments — show what's in the Appointments table
    /// </summary>
    [HttpGet("debug-appointments")]
    public async Task<IActionResult> DebugAppointments()
    {
        var now = DateTime.UtcNow;
        var count = await _context.Appointments.CountAsync();
        var upcoming = await _context.Appointments
            .Where(a => a.Start >= now.Date && a.Start < now.Date.AddDays(4))
            .OrderBy(a => a.Start)
            .Select(a => new { a.ServiceTitanAppointmentId, a.ServiceTitanJobId, a.Start, a.Status, a.TechnicianCount })
            .ToListAsync();
        return Ok(new { totalInTable = count, upcomingWindow = upcoming });
    }

    /// <summary>
    /// GET /migrate/debug-appt-raw — show raw appointments response from ST to inspect technicianAssignments shape
    /// </summary>
    [HttpGet("debug-appt-raw")]
    public async Task<IActionResult> DebugApptRaw()
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var raw = await _stService.GetAppointmentsAsync(today, today.AddDays(4));
            var parsed = JsonSerializer.Deserialize<JsonElement>(raw);

            var items = new List<object>();
            if (parsed.TryGetProperty("data", out var data))
            {
                foreach (var appt in data.EnumerateArray().Take(5))
                {
                    long id = 0, jobId = 0;
                    if (appt.TryGetProperty("id", out var idProp)) id = idProp.GetInt64();
                    if (appt.TryGetProperty("jobId", out var jProp)) jobId = jProp.GetInt64();

                    // Get raw technicianAssignments as a string so we can see its shape
                    string techRaw = "none";
                    if (appt.TryGetProperty("technicianAssignments", out var ta))
                        techRaw = ta.GetRawText();

                    items.Add(new { id, jobId, technicianAssignments = techRaw });
                }
            }

            return Ok(new { count = items.Count, items });
        }
        catch (Exception ex)
        {
            return Ok(new { error = ex.Message });
        }
    }

    /// <summary>
    /// GET /migrate/debug-assignments — show raw assignment export from ST + what's in AppointmentTechnicians table
    /// </summary>
    [HttpGet("debug-assignments")]
    public async Task<IActionResult> DebugAssignments()
    {
        try
        {
            // Check what's in the AppointmentTechnicians table
            int techCount = 0;
            try { techCount = await _context.AppointmentTechnicians.CountAsync(); } catch { }

            // Fetch raw assignment export from ST (starting from today)
            var raw = await _stService.ExportAppointmentAssignmentsAsync(DateTime.UtcNow.Date.ToString("yyyy-MM-dd"));
            var parsed = JsonSerializer.Deserialize<JsonElement>(raw);

            // Pull out first 10 data items for inspection
            var items = new List<object>();
            if (parsed.TryGetProperty("data", out var data))
            {
                foreach (var item in data.EnumerateArray().Take(10))
                {
                    long apptId = 0, techId = 0, jobId = 0;
                    string techName = "";
                    bool active = true;
                    item.TryGetProperty("appointmentId", out var aId); if (aId.ValueKind == JsonValueKind.Number) apptId = aId.GetInt64();
                    item.TryGetProperty("technicianId", out var tId); if (tId.ValueKind == JsonValueKind.Number) techId = tId.GetInt64();
                    item.TryGetProperty("jobId", out var jId); if (jId.ValueKind == JsonValueKind.Number) jobId = jId.GetInt64();
                    item.TryGetProperty("technicianName", out var tName); if (tName.ValueKind == JsonValueKind.String) techName = tName.GetString() ?? "";
                    item.TryGetProperty("active", out var act); if (act.ValueKind == JsonValueKind.False) active = false;
                    items.Add(new { apptId, techId, jobId, techName, active });
                }
            }

            // Get our upcoming appointment ST IDs for comparison
            var upcomingApptIds = await _context.Appointments
                .Where(a => a.Start >= DateTime.UtcNow.Date && a.Start < DateTime.UtcNow.Date.AddDays(4))
                .Select(a => a.ServiceTitanAppointmentId)
                .ToListAsync();

            return Ok(new {
                technicianRowsInDb = techCount,
                upcomingApptIds,
                sampleAssignments = items,
                hasMore = parsed.TryGetProperty("hasMore", out var hm) ? hm.GetBoolean() : (bool?)null
            });
        }
        catch (Exception ex)
        {
            return Ok(new { error = ex.Message });
        }
    }
}