using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Domain.Entities;
using PatriotMechanical.API.Infrastructure.Data;
using PatriotMechanical.API.Application.Services;
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

        // GET /board
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
                .ToListAsync();

            return Ok(columns.Select(col => new
            {
                col.Id,
                col.Name,
                col.Color,
                col.SortOrder,
                col.IsDefault,
                col.ColumnRole,
                col.ServiceTitanHoldReasonId,
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
                    })
                })
            }));
        }

        // GET /board/hold-reasons
        // Returns active hold reasons from ServiceTitan for the Add Column dropdown
        [HttpGet("hold-reasons")]
        public async Task<IActionResult> GetHoldReasons()
        {
            try
            {
                var raw = await _stService.GetHoldReasonsAsync();
                var parsed = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(raw);

                if (!parsed.TryGetProperty("data", out var data))
                    return Ok(new List<object>());

                var result = new List<object>();
                foreach (var reason in data.EnumerateArray())
                {
                    var active = !reason.TryGetProperty("active", out var activeProp) || activeProp.GetBoolean();
                    if (!active) continue;

                    result.Add(new
                    {
                        id = reason.GetProperty("id").GetInt64(),
                        name = reason.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : ""
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HoldReasons] Failed: {ex.Message}");
                return Ok(new List<object>());
            }
        }

        // POST /board/columns
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
                IsDefault = false,
                ColumnRole = string.IsNullOrWhiteSpace(req.ColumnRole) ? null : req.ColumnRole,
                ServiceTitanHoldReasonId = req.ServiceTitanHoldReasonId
            };

            _context.BoardColumns.Add(col);
            await _context.SaveChangesAsync();

            return Ok(new { col.Id, col.Name, col.Color, col.SortOrder, col.ColumnRole, col.ServiceTitanHoldReasonId });
        }

        // DELETE /board/columns/{id}
        [HttpDelete("columns/{id}")]
        public async Task<IActionResult> DeleteColumn(Guid id)
        {
            var col = await _context.BoardColumns.FindAsync(id);
            if (col == null) return NotFound();

            _context.BoardColumns.Remove(col);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Column deleted." });
        }

        // PUT /board/columns/{id}
        [HttpPut("columns/{id}")]
        public async Task<IActionResult> UpdateColumn(Guid id, [FromBody] UpdateColumnRequest req)
        {
            var col = await _context.BoardColumns.FindAsync(id);
            if (col == null) return NotFound();

            if (req.Name != null) col.Name = req.Name;
            if (req.Color != null) col.Color = req.Color;
            if (req.ColumnRole != null) col.ColumnRole = req.ColumnRole == "" ? null : req.ColumnRole;
            if (req.ServiceTitanHoldReasonId.HasValue)
                col.ServiceTitanHoldReasonId = req.ServiceTitanHoldReasonId == 0 ? null : req.ServiceTitanHoldReasonId;

            await _context.SaveChangesAsync();
            return Ok(new { col.Id, col.Name, col.Color, col.ColumnRole, col.ServiceTitanHoldReasonId });
        }

        // POST /board/cards
        [HttpPost("cards")]
        public async Task<IActionResult> AddCard([FromBody] AddCardRequest req)
        {
            var column = await _context.BoardColumns.FindAsync(req.ColumnId);
            if (column == null) return BadRequest(new { message = "Column not found." });

            var exists = await _context.BoardCards.AnyAsync(c => c.JobNumber == req.JobNumber);
            if (exists) return BadRequest(new { message = $"Job {req.JobNumber} is already on the board." });

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

            if (!string.IsNullOrWhiteSpace(req.Note))
            {
                var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "User";
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

        // DELETE /board/cards/{id}
        [HttpDelete("cards/{id}")]
        public async Task<IActionResult> RemoveCard(Guid id)
        {
            var card = await _context.BoardCards.FindAsync(id);
            if (card == null) return NotFound();
            _context.BoardCards.Remove(card);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Card removed." });
        }

        // PUT /board/cards/{id}/move
        [HttpPut("cards/{id}/move")]
        public async Task<IActionResult> MoveCard(Guid id, [FromBody] MoveCardRequest req)
        {
            var card = await _context.BoardCards.FindAsync(id);
            if (card == null) return NotFound();

            card.BoardColumnId = req.ColumnId;
            card.SortOrder = req.SortOrder;

            await _context.SaveChangesAsync();
            return Ok(new { card.Id, card.BoardColumnId, card.SortOrder });
        }

        // POST /board/cards/{id}/notes
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

        // POST /board/migrate/assign-roles
        [HttpPost("migrate/assign-roles")]
        public async Task<IActionResult> AssignRolesByName()
        {
            var columns = await _context.BoardColumns.ToListAsync();
            int updated = 0;

            foreach (var col in columns)
            {
                if (col.ColumnRole != null) continue;

                var lower = col.Name.ToLower();

                if (lower.Contains("schedule") || lower.Contains("waiting to schedule"))
                {
                    col.ColumnRole = "WaitingToSchedule";
                    updated++;
                }
                else if (lower.Contains("return") || lower.Contains("need to return"))
                {
                    col.ColumnRole = "NeedToReturn";
                    updated++;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = $"Assigned roles to {updated} column(s)." });
        }
    }

    // ─── Request DTOs ─────────────────────────────────────────────

    public class AddColumnRequest
    {
        public string Name { get; set; } = null!;
        public string? Color { get; set; }
        public string? ColumnRole { get; set; }
        public long? ServiceTitanHoldReasonId { get; set; }
    }

    public class UpdateColumnRequest
    {
        public string? Name { get; set; }
        public string? Color { get; set; }
        public string? ColumnRole { get; set; }
        public long? ServiceTitanHoldReasonId { get; set; }
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