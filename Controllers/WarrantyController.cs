using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Domain.Entities;
using PatriotMechanical.API.Infrastructure.Data;
using System.Security.Claims;

namespace PatriotMechanical.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("warranty")]
    public class WarrantyController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WarrantyController(AppDbContext context)
        {
            _context = context;
        }

        // GET /warranty — all open claims
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool includeClosed = false)
        {
            var isDemo = DemoFilter.IsDemo(User);

            var query = _context.WarrantyClaims
                .Where(w => !isDemo || w.IsDemo)
                .Where(w => isDemo || !w.IsDemo)
                .AsQueryable();

            if (!includeClosed)
                query = query.Where(w => !w.IsClosed);

            var claims = await query
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new
                {
                    w.Id, w.PartName, w.PartModelNumber, w.CustomerName, w.JobNumber,
                    w.Supplier, w.Manufacturer, w.RmaNumber, w.Status, w.ClaimType,
                    w.CreditAmount, w.ExpectedShipDate, w.DefectivePartReturned, w.IsClosed,
                    w.CreatedAt, w.ClaimFiledDate, w.ApprovedDate, w.PartReceivedDate,
                    w.InstalledDate, w.DefectiveReturnedDate, w.ClosedDate,
                    NoteCount = w.Notes.Count
                })
                .ToListAsync();

            return Ok(claims);
        }

        // GET /warranty/{id} — single claim with notes
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var claim = await _context.WarrantyClaims
                .Include(w => w.Notes.OrderByDescending(n => n.CreatedAt))
                .FirstOrDefaultAsync(w => w.Id == id);

            if (claim == null) return NotFound();

            return Ok(new
            {
                claim.Id, claim.PartName, claim.PartModelNumber, claim.PartSerialNumber,
                claim.UnitModelNumber, claim.UnitSerialNumber,
                claim.CustomerName, claim.JobNumber, claim.ReturnJobNumber,
                claim.Supplier, claim.Manufacturer, claim.RmaNumber,
                claim.Status, claim.ClaimType, claim.CreditAmount,
                claim.CreatedAt, claim.ClaimFiledDate, claim.ApprovedDate,
                claim.ExpectedShipDate, claim.PartReceivedDate, claim.InstalledDate,
                claim.DefectiveReturnedDate, claim.ClosedDate,
                claim.DefectivePartReturned, claim.IsClosed,
                Notes = claim.Notes.Select(n => new { n.Id, n.Text, n.Author, n.CreatedAt })
            });
        }

        // POST /warranty — create new claim
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateWarrantyRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.PartName))
                return BadRequest(new { message = "Part name is required." });

            // Try to resolve customer name from job number
            string? customerName = req.CustomerName;
            Guid? customerId = null;
            if (!string.IsNullOrWhiteSpace(req.JobNumber))
            {
                var wo = await _context.WorkOrders
                    .Include(w => w.Customer)
                    .FirstOrDefaultAsync(w => w.JobNumber == req.JobNumber);
                if (wo != null)
                {
                    customerName = customerName ?? wo.Customer?.Name;
                    customerId = wo.CustomerId;
                }
            }

            var claim = new WarrantyClaim
            {
                Id = Guid.NewGuid(),
                PartName = req.PartName,
                PartModelNumber = req.PartModelNumber,
                PartSerialNumber = req.PartSerialNumber,
                UnitModelNumber = req.UnitModelNumber,
                UnitSerialNumber = req.UnitSerialNumber,
                CustomerId = customerId,
                CustomerName = customerName,
                JobNumber = req.JobNumber,
                Supplier = req.Supplier,
                Manufacturer = req.Manufacturer,
                ClaimType = req.ClaimType ?? "Replacement",
                Status = "Diagnosis",
                IsDemo = DemoFilter.IsDemo(User)
            };

            _context.WarrantyClaims.Add(claim);

            // Add initial note if provided
            if (!string.IsNullOrWhiteSpace(req.Note))
            {
                var author = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
                _context.WarrantyClaimNotes.Add(new WarrantyClaimNote
                {
                    Id = Guid.NewGuid(),
                    WarrantyClaimId = claim.Id,
                    Text = req.Note,
                    Author = author
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { claim.Id, claim.PartName, claim.Status });
        }

        // PUT /warranty/{id}/status — advance or set status
        [HttpPut("{id}/status")]
        public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest req)
        {
            var claim = await _context.WarrantyClaims.FindAsync(id);
            if (claim == null) return NotFound();

            claim.Status = req.Status;

            // Auto-set date fields based on status
            var now = DateTime.UtcNow;
            switch (req.Status)
            {
                case "Claim Filed": claim.ClaimFiledDate ??= now; break;
                case "Approved": claim.ApprovedDate ??= now; break;
                case "Part Received": claim.PartReceivedDate ??= now; break;
                case "Installed": claim.InstalledDate ??= now; break;
                case "Defective Returned": claim.DefectiveReturnedDate ??= now; claim.DefectivePartReturned = true; break;
                case "Closed": claim.ClosedDate ??= now; claim.IsClosed = true; break;
            }

            // Auto-add status change note
            var author = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
            _context.WarrantyClaimNotes.Add(new WarrantyClaimNote
            {
                Id = Guid.NewGuid(),
                WarrantyClaimId = id,
                Text = $"Status changed to: {req.Status}",
                Author = author
            });

            await _context.SaveChangesAsync();
            return Ok(new { claim.Id, claim.Status });
        }

        // PUT /warranty/{id} — update claim details
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWarrantyRequest req)
        {
            var claim = await _context.WarrantyClaims.FindAsync(id);
            if (claim == null) return NotFound();

            if (req.RmaNumber != null) claim.RmaNumber = req.RmaNumber;
            if (req.Supplier != null) claim.Supplier = req.Supplier;
            if (req.Manufacturer != null) claim.Manufacturer = req.Manufacturer;
            if (req.ReturnJobNumber != null) claim.ReturnJobNumber = req.ReturnJobNumber;
            if (req.ClaimType != null) claim.ClaimType = req.ClaimType;
            if (req.CreditAmount.HasValue) claim.CreditAmount = req.CreditAmount;
            if (req.ExpectedShipDate.HasValue)
                claim.ExpectedShipDate = DateTime.SpecifyKind(req.ExpectedShipDate.Value, DateTimeKind.Utc);

            await _context.SaveChangesAsync();
            return Ok(new { message = "Claim updated." });
        }

        // POST /warranty/{id}/notes
        [HttpPost("{id}/notes")]
        public async Task<IActionResult> AddNote(Guid id, [FromBody] AddNoteRequest req)
        {
            var exists = await _context.WarrantyClaims.AnyAsync(w => w.Id == id);
            if (!exists) return NotFound();

            var author = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
            var note = new WarrantyClaimNote
            {
                Id = Guid.NewGuid(),
                WarrantyClaimId = id,
                Text = req.Text,
                Author = author
            };

            _context.WarrantyClaimNotes.Add(note);
            await _context.SaveChangesAsync();
            return Ok(new { note.Id, note.Text, note.Author, note.CreatedAt });
        }

        // DELETE /warranty/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var claim = await _context.WarrantyClaims.Include(c => c.Notes).FirstOrDefaultAsync(c => c.Id == id);
            if (claim == null) return NotFound();
            _context.WarrantyClaims.Remove(claim);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Claim deleted." });
        }
    }

    // ─── DTOs ─────────────────────────────────────────────────────

    public class CreateWarrantyRequest
    {
        public string PartName { get; set; } = null!;
        public string? PartModelNumber { get; set; }
        public string? PartSerialNumber { get; set; }
        public string? UnitModelNumber { get; set; }
        public string? UnitSerialNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? JobNumber { get; set; }
        public string? Supplier { get; set; }
        public string? Manufacturer { get; set; }
        public string? ClaimType { get; set; }
        public string? Note { get; set; }
    }

    public class UpdateStatusRequest
    {
        public string Status { get; set; } = null!;
    }

    public class UpdateWarrantyRequest
    {
        public string? RmaNumber { get; set; }
        public string? Supplier { get; set; }
        public string? Manufacturer { get; set; }
        public string? ReturnJobNumber { get; set; }
        public string? ClaimType { get; set; }
        public decimal? CreditAmount { get; set; }
        public DateTime? ExpectedShipDate { get; set; }
    }
}