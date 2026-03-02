using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("workorders")]
    public class WorkOrderController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WorkOrderController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("{jobNumber}")]
        public async Task<IActionResult> GetByJobNumber(string jobNumber)
        {
            var wo = await _context.WorkOrders
                .Include(w => w.Customer)
                .Include(w => w.Invoice)
                .FirstOrDefaultAsync(w => w.JobNumber == jobNumber);

            if (wo == null) return NotFound();

            // Get board card info if on the board
            var boardCard = await _context.BoardCards
                .Include(c => c.BoardColumn)
                .FirstOrDefaultAsync(c => c.JobNumber == jobNumber);

            // Get subcontractor entries
            var subEntries = await _context.SubcontractorEntries
                .Include(e => e.Subcontractor)
                .Where(e => e.WorkOrderId == wo.Id)
                .Select(e => new
                {
                    SubcontractorName = e.Subcontractor.Name,
                    e.Hours,
                    e.HourlyRate,
                    Cost = e.Hours * e.HourlyRate,
                    e.Date
                })
                .ToListAsync();

            // Equipment at customer site
            var equipment = await _context.Equipment
                .Where(e => e.CustomerId == wo.CustomerId)
                .Select(e => new { e.Type, e.Brand, e.ModelNumber, e.SerialNumber })
                .ToListAsync();

            return Ok(new
            {
                wo.JobNumber,
                wo.Status,
                wo.JobTypeName,
                CustomerName = wo.Customer?.Name,
                wo.CustomerId,
                wo.CreatedAt,
                wo.CompletedAt,
                wo.Subtotal,
                wo.Tax,
                wo.TotalAmount,
                wo.TotalLaborCost,
                wo.TotalMaterialCost,
                wo.GrossProfit,
                wo.MarginPercent,
                Invoice = wo.Invoice != null ? new
                {
                    wo.Invoice.InvoiceNumber,
                    wo.Invoice.TotalAmount,
                    wo.Invoice.BalanceRemaining,
                    wo.Invoice.Status
                } : null,
                BoardColumn = boardCard != null ? new
                {
                    boardCard.BoardColumn.Name,
                    boardCard.BoardColumn.Color
                } : null,
                SubcontractorEntries = subEntries,
                Equipment = equipment
            });
        }
    }
}