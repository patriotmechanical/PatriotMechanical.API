using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Domain.Entities;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("subcontractors")]
    public class SubcontractorController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SubcontractorController(AppDbContext context)
        {
            _context = context;
        }

        // GET /subcontractors — list all active subs
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var isDemo = DemoFilter.IsDemo(User);
            var subs = await _context.Subcontractors
                .Where(s => s.IsActive)
                .Where(s => !isDemo || s.Name.StartsWith("[DEMO]"))
                .Where(s => isDemo || !s.Name.StartsWith("[DEMO]"))
                .OrderBy(s => s.Name)
                .Select(s => new
                {
                    s.Id,
                    s.Name,
                    s.Company,
                    s.Trade,
                    TotalHours = s.Entries.Sum(e => (decimal?)e.Hours) ?? 0,
                    TotalCost = s.Entries.Sum(e => (decimal?)(e.Hours * e.HourlyRate)) ?? 0
                })
                .ToListAsync();

            return Ok(subs);
        }

        // POST /subcontractors — create a new sub
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Subcontractor sub)
        {
            if (string.IsNullOrWhiteSpace(sub.Name))
                return BadRequest("Name is required.");

            sub.Id = Guid.NewGuid();
            sub.IsActive = true;
            _context.Subcontractors.Add(sub);
            await _context.SaveChangesAsync();

            return Ok(new { sub.Id, sub.Name, sub.Company, sub.Trade });
        }

        // GET /subcontractors/{id}/entries — all entries for a sub
        [HttpGet("{id}/entries")]
        public async Task<IActionResult> GetEntriesForSub(Guid id)
        {
            var sub = await _context.Subcontractors.FindAsync(id);
            if (sub == null) return NotFound();

            var entries = await _context.SubcontractorEntries
                .Where(e => e.SubcontractorId == id)
                .Include(e => e.WorkOrder)
                .ThenInclude(w => w.Customer)
                .OrderByDescending(e => e.Date)
                .Select(e => new
                {
                    e.Id,
                    e.Date,
                    e.Hours,
                    e.HourlyRate,
                    TotalCost = e.Hours * e.HourlyRate,
                    e.Notes,
                    JobNumber = e.WorkOrder.JobNumber,
                    CustomerName = e.WorkOrder.Customer.Name,
                    WorkOrderId = e.WorkOrderId
                })
                .ToListAsync();

            return Ok(new
            {
                sub.Id,
                sub.Name,
                sub.Company,
                sub.Trade,
                TotalHours = entries.Sum(e => e.Hours),
                TotalCost = entries.Sum(e => e.TotalCost),
                Entries = entries
            });
        }

        // GET /subcontractors/by-job/{workOrderId} — all sub entries for a job
        [HttpGet("by-job/{workOrderId}")]
        public async Task<IActionResult> GetEntriesForJob(Guid workOrderId)
        {
            var workOrder = await _context.WorkOrders
                .Include(w => w.Customer)
                .FirstOrDefaultAsync(w => w.Id == workOrderId);

            if (workOrder == null) return NotFound();

            var entries = await _context.SubcontractorEntries
                .Where(e => e.WorkOrderId == workOrderId)
                .Include(e => e.Subcontractor)
                .OrderByDescending(e => e.Date)
                .Select(e => new
                {
                    e.Id,
                    e.Date,
                    e.Hours,
                    e.HourlyRate,
                    TotalCost = e.Hours * e.HourlyRate,
                    e.Notes,
                    SubName = e.Subcontractor.Name,
                    SubCompany = e.Subcontractor.Company,
                    SubId = e.SubcontractorId
                })
                .ToListAsync();

            return Ok(new
            {
                workOrder.Id,
                workOrder.JobNumber,
                CustomerName = workOrder.Customer.Name,
                TotalSubHours = entries.Sum(e => e.Hours),
                TotalSubCost = entries.Sum(e => e.TotalCost),
                Entries = entries
            });
        }

        // POST /subcontractors/entries — log hours
        [HttpPost("entries")]
        public async Task<IActionResult> LogHours([FromBody] SubcontractorEntry entry)
        {
            if (entry.SubcontractorId == Guid.Empty) return BadRequest("Subcontractor is required.");
            if (entry.WorkOrderId == Guid.Empty) return BadRequest("Work order is required.");
            if (entry.Hours <= 0) return BadRequest("Hours must be greater than 0.");
            if (entry.HourlyRate <= 0) return BadRequest("Hourly rate must be greater than 0.");

            var subExists = await _context.Subcontractors.AnyAsync(s => s.Id == entry.SubcontractorId);
            if (!subExists) return BadRequest("Subcontractor not found.");

            var jobExists = await _context.WorkOrders.AnyAsync(w => w.Id == entry.WorkOrderId);
            if (!jobExists) return BadRequest("Work order not found.");

            entry.Id = Guid.NewGuid();
            entry.Date = DateTime.SpecifyKind(entry.Date, DateTimeKind.Utc);
            entry.CreatedAt = DateTime.UtcNow;

            _context.SubcontractorEntries.Add(entry);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                entry.Id,
                entry.Hours,
                entry.HourlyRate,
                TotalCost = entry.Hours * entry.HourlyRate,
                entry.Date,
                entry.Notes
            });
        }

        // DELETE /subcontractors/entries/{id}
        [HttpDelete("entries/{id}")]
        public async Task<IActionResult> DeleteEntry(Guid id)
        {
            var entry = await _context.SubcontractorEntries.FindAsync(id);
            if (entry == null) return NotFound();

            _context.SubcontractorEntries.Remove(entry);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}