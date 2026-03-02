using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PatriotMechanical.API.Application.Services;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("debug")]
    public class DebugController : ControllerBase
    {
        private readonly ServiceTitanService _service;
        private readonly AppDbContext _context;

        public DebugController(ServiceTitanService service, AppDbContext context)
        {
            _service = service;
            _context = context;
        }

        // Fetch a specific job directly from ServiceTitan by ST job ID
        [HttpGet("raw-job/{jobNumber}")]
        public async Task<IActionResult> GetRawJobByNumber(string jobNumber)
        {
            var stored = await _context.WorkOrders
                .Where(w => w.JobNumber == jobNumber)
                .Select(w => new { w.JobNumber, w.CompletedAt, w.CreatedAt, w.ServiceTitanJobId, w.JobTypeName })
                .FirstOrDefaultAsync();

            if (stored == null) return NotFound();

            // Also fetch directly from ServiceTitan API to see raw completedOn
            var raw = await _service.GetRawJobByIdAsync(stored.ServiceTitanJobId);

            return Ok(new { stored, rawFromServiceTitan = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(raw) });
        }

        [HttpGet("job-type-map")]
        public async Task<IActionResult> GetJobTypeMap()
        {
            var map = await _service.GetJobTypeMapAsync();
            return Ok(map);
        }

        [HttpGet("job-types-stored")]
        public async Task<IActionResult> GetStoredJobTypes()
        {
            var types = await _context.WorkOrders
                .GroupBy(w => w.JobTypeName)
                .Select(g => new { JobTypeName = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            return Ok(types);
        }

        [HttpGet("completed-at-stats")]
        public async Task<IActionResult> GetCompletedAtStats()
        {
            var total = await _context.WorkOrders.CountAsync();
            var withDate = await _context.WorkOrders.CountAsync(w => w.CompletedAt != null);
            var earliest = await _context.WorkOrders
                .Where(w => w.CompletedAt != null)
                .MinAsync(w => w.CompletedAt);
            var latest = await _context.WorkOrders
                .Where(w => w.CompletedAt != null)
                .MaxAsync(w => w.CompletedAt);

            return Ok(new { total, withCompletedAt = withDate, earliest, latest });
        }

        // Backfill CreatedAt for ALL work orders from ServiceTitan
        [HttpPost("backfill-created-dates")]
        public async Task<IActionResult> BackfillCreatedDates()
        {
            var allJobs = await _context.WorkOrders.ToListAsync();
            int updated = 0, failed = 0;

            foreach (var wo in allJobs)
            {
                try
                {
                    var raw = await _service.GetRawJobByIdAsync(wo.ServiceTitanJobId);
                    var job = JsonSerializer.Deserialize<JsonElement>(raw);

                    if (job.TryGetProperty("createdOn", out var createdProp) &&
                        createdProp.ValueKind == JsonValueKind.String &&
                        DateTime.TryParse(createdProp.GetString(), null,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out var parsedDate))
                    {
                        wo.CreatedAt = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
                        updated++;
                    }

                    // Also fix status while we're here
                    if (job.TryGetProperty("jobStatus", out var statusProp))
                        wo.Status = statusProp.GetString() ?? wo.Status;
                }
                catch { failed++; }
            }

            await _context.SaveChangesAsync();
            return Ok(new { total = allJobs.Count, updated, failed });
        }

        // Backfill CompletedAt for all PM jobs by fetching each one individually from ST
        [HttpPost("backfill-job-types")]
        public async Task<IActionResult> BackfillJobTypes()
        {
            var pmKeywords = new[] { "maintenance", "tune up", "tune-up", "pm" };

            var pmJobs = await _context.WorkOrders
                .Where(w => w.JobTypeName != null &&
                    pmKeywords.Any(k => w.JobTypeName.ToLower().Contains(k)))
                .ToListAsync();

            int updated = 0;
            int failed = 0;

            foreach (var wo in pmJobs)
            {
                try
                {
                    var raw = await _service.GetRawJobByIdAsync(wo.ServiceTitanJobId);
                    var job = JsonSerializer.Deserialize<JsonElement>(raw);

                    if (job.TryGetProperty("completedOn", out var completedProp) &&
                        completedProp.ValueKind == JsonValueKind.String &&
                        DateTime.TryParse(completedProp.GetString(), null,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out var parsedDate))
                    {
                        wo.CompletedAt = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
                        updated++;
                    }
                }
                catch
                {
                    failed++;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { pmJobsFound = pmJobs.Count, updated, failed });
        }

        // Inspect raw appointment data for a specific job
        [HttpGet("appointments/{jobNumber}")]
        public async Task<IActionResult> GetJobAppointments(string jobNumber)
        {
            var wo = await _context.WorkOrders
                .Where(w => w.JobNumber == jobNumber)
                .FirstOrDefaultAsync();

            if (wo == null) return NotFound($"Job {jobNumber} not found");

            var raw = await _service.GetAppointmentsForJobAsync(wo.ServiceTitanJobId);
            var parsed = JsonSerializer.Deserialize<JsonElement>(raw);

            // Also get tech assignments
            string? assignRaw = null;
            JsonElement? assignParsed = null;
            try
            {
                assignRaw = await _service.GetAppointmentAssignmentsForJobAsync(wo.ServiceTitanJobId);
                assignParsed = JsonSerializer.Deserialize<JsonElement>(assignRaw);
            }
            catch { }

            return Ok(new
            {
                jobNumber = wo.JobNumber,
                serviceTitanJobId = wo.ServiceTitanJobId,
                status = wo.Status,
                appointments = parsed,
                technicianAssignments = assignParsed
            });
        }
    }
}