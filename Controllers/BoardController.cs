using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Application.Services;
using PatriotMechanical.API.Domain.Entities;
using PatriotMechanical.API.Infrastructure.Data;
using System.Security.Claims;

namespace PatriotMechanical.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("board")]
    public class BoardController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ServiceTitanService _stService;

        public BoardController(AppDbContext context, ServiceTitanService stService)
        {
            _context = context;
            _stService = stService;
        }

        // ── ST HOLD REASON MAPPING ───────────────────────────────────
        // Board column name → ST job hold reason ID
        // IDs filled in after running GET /servicetitan/hold-reasons
        // Columns NOT listed here (Waiting to Schedule, Quote Sent) → do nothing in ST
        private static readonly Dictionary<string, long> ColumnHoldReasonMap = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Need to Return",  6275 }, // ST: "Need to return"
            { "Waiting Parts",   1750 }, // ST: "Waiting for materials"
            { "Parts on Order",  6154 }, // ST: "Order Parts"
            { "Waiting Quote",   6153 }, // ST: "Needs Quote"
        };

        // GET /board — full board with columns and cards
        [HttpGet]
        public async Task<IActionResult> GetBoard()
        {
            var isDemo = DemoFilter.IsDemo(User);

            var columns = await _context.BoardColumns
                .OrderBy(c => c.SortOrder)
                .Include(c => c.Cards.Where(card => !isDemo || card.CustomerName.StartsWith("[DEMO]"))
                                     .Where(card => isDemo || !card.CustomerName.StartsWith("[DEMO]"))
                                     .OrderBy(card => card.SortOrder))
                    .ThenInclude(card => card.Notes.OrderByDescending(n => n.CreatedAt))
                .Include(c => c.Cards)
                    .ThenInclude(card => card.WorkOrder)
                        .ThenInclude(wo => wo!.MaterialEntries)
                .Include(c => c.Cards)
                    .ThenInclude(card => card.WorkOrder)
                        .ThenInclude(wo => wo!.Invoice)
                .ToListAsync();

            // Seed defaults if empty
            if (columns.Count == 0)
            {
                var defaults = new[]
                {
                    ("Waiting to Schedule", "#2563eb", 0),
                    ("Need to Return", "#dc2626", 1),
                    ("Waiting Parts", "#d97706", 2),
                    ("Parts on Order", "#ea580c", 3),
                    ("Waiting Quote", "#9333ea", 4),
                    ("Quote Sent", "#16a34a", 5)
                };

                foreach (var (name, color, order) in defaults)
                {
                    _context.BoardColumns.Add(new BoardColumn
                    {
                        Id = Guid.NewGuid(),
                        Name = name,
                        Color = color,
                        SortOrder = order,
                        IsDefault = true
                    });
                }

                await _context.SaveChangesAsync();

                columns = await _context.BoardColumns
                    .OrderBy(c => c.SortOrder)
                    .Include(c => c.Cards)
                    .ToListAsync();
            }

            return Ok(columns.Select(col => new
            {
                col.Id,
                col.Name,
                col.Color,
                col.SortOrder,
                col.IsDefault,
                Cards = col.Cards.Select(card => new
                {
                    card.Id,
                    card.JobNumber,
                    card.CustomerName,
                    card.AddedAt,
                    card.SortOrder,
                    card.BoardColumnId,
                    Notes = card.Notes.Select(n => new
                    {
                        n.Id,
                        n.Text,
                        n.Author,
                        n.CreatedAt
                    }),
                    // WO risk data
                    WoCreatedAt = card.WorkOrder != null ? card.WorkOrder.CreatedAt : (DateTime?)null,
                    WoStatus = card.WorkOrder != null ? card.WorkOrder.Status : null,
                    WoTotal = card.WorkOrder != null ? card.WorkOrder.TotalAmount : 0m,
                    WoHasMaterials = card.WorkOrder != null && card.WorkOrder.MaterialEntries.Any(),
                    WoHasInvoice = card.WorkOrder != null && card.WorkOrder.Invoice != null,
                    WoLastNote = card.Notes.Any() ? card.Notes.Max(n => n.CreatedAt) : (DateTime?)null
                })
            }));
        }

        // POST /board/columns — add a new column
        [HttpPost("columns")]
        public async Task<IActionResult> AddColumn([FromBody] AddColumnRequest req)
        {
            var maxOrder = await _context.BoardColumns.MaxAsync(c => (int?)c.SortOrder) ?? -1;

            var col = new BoardColumn
            {
                Id = Guid.NewGuid(),
                Name = req.Name,
                Color = req.Color ?? "#334155",
                SortOrder = maxOrder + 1,
                IsDefault = false
            };

            _context.BoardColumns.Add(col);
            await _context.SaveChangesAsync();

            return Ok(new { col.Id, col.Name, col.Color, col.SortOrder });
        }

        // DELETE /board/columns/{id}
        [HttpDelete("columns/{id}")]
        public async Task<IActionResult> DeleteColumn(Guid id)
        {
            var col = await _context.BoardColumns.FindAsync(id);
            if (col == null) return NotFound();
            if (col.IsDefault) return BadRequest(new { message = "Cannot delete a default column." });

            _context.BoardColumns.Remove(col);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Column deleted." });
        }

        // PUT /board/columns/{id} — rename or recolor
        [HttpPut("columns/{id}")]
        public async Task<IActionResult> UpdateColumn(Guid id, [FromBody] UpdateColumnRequest req)
        {
            var col = await _context.BoardColumns.FindAsync(id);
            if (col == null) return NotFound();

            if (req.Name != null) col.Name = req.Name;
            if (req.Color != null) col.Color = req.Color;

            await _context.SaveChangesAsync();
            return Ok(new { col.Id, col.Name, col.Color });
        }

        // POST /board/cards — add a work order card to a column
        [HttpPost("cards")]
        public async Task<IActionResult> AddCard([FromBody] AddCardRequest req)
        {
            var column = await _context.BoardColumns.FindAsync(req.ColumnId);
            if (column == null) return BadRequest(new { message = "Column not found." });

            // Check for duplicate job number on board
            var exists = await _context.BoardCards.AnyAsync(c => c.JobNumber == req.JobNumber);
            if (exists) return BadRequest(new { message = $"Job {req.JobNumber} is already on the board." });

            // Try to match to a synced work order
            var wo = await _context.WorkOrders
                .Include(w => w.Customer)
                .FirstOrDefaultAsync(w => w.JobNumber == req.JobNumber);

            var maxOrder = await _context.BoardCards
                .Where(c => c.BoardColumnId == req.ColumnId)
                .MaxAsync(c => (int?)c.SortOrder) ?? -1;

            var card = new BoardCard
            {
                Id = Guid.NewGuid(),
                BoardColumnId = req.ColumnId,
                WorkOrderId = wo?.Id,
                JobNumber = req.JobNumber,
                CustomerName = wo?.Customer?.Name ?? req.CustomerName ?? "Unknown",
                SortOrder = maxOrder + 1
            };

            _context.BoardCards.Add(card);

            // Add initial note if provided
            if (!string.IsNullOrWhiteSpace(req.Note))
            {
                var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";
                _context.BoardCardNotes.Add(new BoardCardNote
                {
                    Id = Guid.NewGuid(),
                    BoardCardId = card.Id,
                    Text = req.Note,
                    Author = userName
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                card.Id,
                card.JobNumber,
                card.CustomerName,
                card.AddedAt,
                card.SortOrder,
                card.BoardColumnId
            });
        }

        // PUT /board/cards/{id}/move — move card to different column (drag & drop)
        [HttpPut("cards/{id}/move")]
        public async Task<IActionResult> MoveCard(Guid id, [FromBody] MoveCardRequest req)
        {
            var card = await _context.BoardCards.FindAsync(id);
            if (card == null) return NotFound();

            card.BoardColumnId = req.ColumnId;
            card.SortOrder = req.SortOrder;

            await _context.SaveChangesAsync();

            // ── Push hold reason to ServiceTitan (fire-and-forget, don't fail the card move) ──
            _ = Task.Run(async () =>
            {
                try
                {
                    var column = await _context.BoardColumns.FindAsync(req.ColumnId);
                    if (column == null) return;

                    // If column is not mapped, do nothing in ST
                    if (!ColumnHoldReasonMap.TryGetValue(column.Name.Trim(), out var holdReasonId) || holdReasonId == 0)
                        return;

                    // Find the active/scheduled appointment for this job
                    var wo = await _context.WorkOrders.FirstOrDefaultAsync(w => w.JobNumber == card.JobNumber);
                    if (wo == null || wo.ServiceTitanJobId == 0) return;

                    var appt = await _context.Appointments
                        .Where(a => a.ServiceTitanJobId == wo.ServiceTitanJobId
                            && (a.Status == "Scheduled" || a.Status == "Dispatched" || a.Status == "Working"))
                        .OrderByDescending(a => a.Start)
                        .FirstOrDefaultAsync();

                    if (appt == null) return;

                    var memo = $"Moved to {column.Name.Trim()} on {DateTime.Now:MM/dd/yyyy}";
                    await _stService.PutAppointmentOnHoldAsync(appt.ServiceTitanAppointmentId, holdReasonId, memo);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ST Hold Sync] Failed for card {id}: {ex.Message}");
                }
            });

            return Ok(new { message = "Card moved." });
        }

        // DELETE /board/cards/{id}
        [HttpDelete("cards/{id}")]
        public async Task<IActionResult> DeleteCard(Guid id)
        {
            var card = await _context.BoardCards.FindAsync(id);
            if (card == null) return NotFound();

            _context.BoardCards.Remove(card);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Card removed." });
        }

        // POST /board/cards/{id}/notes — add a timestamped note
        [HttpPost("cards/{id}/notes")]
        public async Task<IActionResult> AddNote(Guid id, [FromBody] AddNoteRequest req)
        {
            var card = await _context.BoardCards.FindAsync(id);
            if (card == null) return NotFound();

            var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "System";

            var note = new BoardCardNote
            {
                Id = Guid.NewGuid(),
                BoardCardId = id,
                Text = req.Text,
                Author = userName
            };

            _context.BoardCardNotes.Add(note);
            await _context.SaveChangesAsync();

            return Ok(new { note.Id, note.Text, note.Author, note.CreatedAt });
        }

        // GET /board/cards/{id}/notes
        [HttpGet("cards/{id}/notes")]
        public async Task<IActionResult> GetNotes(Guid id)
        {
            var notes = await _context.BoardCardNotes
                .Where(n => n.BoardCardId == id)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new { n.Id, n.Text, n.Author, n.CreatedAt })
                .ToListAsync();

            return Ok(notes);
        }
    }

    // ─── Request DTOs ─────────────────────────────────────────────

    public class AddColumnRequest
    {
        public string Name { get; set; } = null!;
        public string? Color { get; set; }
    }

    public class UpdateColumnRequest
    {
        public string? Name { get; set; }
        public string? Color { get; set; }
    }

    public class AddCardRequest
    {
        public Guid ColumnId { get; set; }
        public string JobNumber { get; set; } = null!;
        public string? CustomerName { get; set; }
        public string? Note { get; set; }
    }

    public class MoveCardRequest
    {
        public Guid ColumnId { get; set; }
        public int SortOrder { get; set; }
    }

    public class AddNoteRequest
    {
        public string Text { get; set; } = null!;
    }
}