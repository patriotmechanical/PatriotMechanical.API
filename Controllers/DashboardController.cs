using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Controllers;

[Authorize]
[ApiController]
[Route("dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var isDemo = DemoFilter.IsDemo(User);

        var ar = await _context.Invoices
            .Where(i => i.BalanceRemaining > 0)
            .Where(i => !isDemo || i.Customer.Name.StartsWith("[DEMO]"))
            .Where(i => isDemo || !i.Customer.Name.StartsWith("[DEMO]"))
            .GroupBy(i => i.Customer.Name)
            .Select(g => new
            {
                Name = g.Key,
                TotalOwed = g.Sum(i => i.BalanceRemaining)
            })
            .OrderByDescending(x => x.TotalOwed)
            .ToListAsync();

        var ap = await _context.ApBills
            .Where(b => !b.IsPaid)
            .Where(b => !isDemo || b.Vendor.Name.StartsWith("[DEMO]"))
            .Where(b => isDemo || !b.Vendor.Name.StartsWith("[DEMO]"))
            .GroupBy(b => b.Vendor.Name)
            .Select(g => new
            {
                Name = g.Key,
                TotalOwed = g.Sum(b => b.Amount),
                TotalInvoiceAmount = g.Sum(b => b.TotalAmount),
                NextDue = g.Min(b => b.DueDate)
            })
            .OrderByDescending(x => x.TotalOwed)
            .ToListAsync();

        var totalAr = ar.Sum(x => x.TotalOwed);
        var totalAp = ap.Sum(x => x.TotalInvoiceAmount);

        var openWorkOrders = await _context.WorkOrders
            .Where(w => w.Status != null
                && (w.Status.ToLower().Contains("inprogress")
                    || w.Status.ToLower().Contains("scheduled")
                    || w.Status.ToLower().Contains("hold")))
            .Where(w => !isDemo || w.Customer.Name.StartsWith("[DEMO]"))
            .Where(w => isDemo || w.Customer == null || !w.Customer.Name.StartsWith("[DEMO]"))
            .Include(w => w.Customer)
            .OrderBy(w => w.CreatedAt)
            .Select(w => new
            {
                w.Id,
                w.JobNumber,
                CustomerName = w.Customer.Name,
                w.Status,
                w.CreatedAt,
                w.TotalAmount
            })
            .ToListAsync();

        // Board column counts with card details for ops stats
        var boardColumns = await _context.BoardColumns
            .OrderBy(c => c.SortOrder)
            .Select(c => new
            {
                c.Name,
                c.Color,
                Cards = c.Cards
                    .Where(card => !isDemo || card.CustomerName.StartsWith("[DEMO]"))
                    .Where(card => isDemo || !card.CustomerName.StartsWith("[DEMO]"))
                    .Select(card => new { card.JobNumber, card.CustomerName })
                    .ToList()
            })
            .ToListAsync();

        // PM overdue count
        var pmKeywords = new[] { "maintenance", "tune up", "tune-up", "pm" };
        var pmCustomers = await _context.WorkOrders
            .Where(w => w.JobTypeName != null && pmKeywords.Any(k => w.JobTypeName.ToLower().Contains(k)))
            .Where(w => !isDemo || w.Customer.Name.StartsWith("[DEMO]"))
            .Where(w => isDemo || w.Customer == null || !w.Customer.Name.StartsWith("[DEMO]"))
            .Where(w => w.CompletedAt != null)
            .Include(w => w.Customer)
            .GroupBy(w => new { w.CustomerId, w.Customer.Name })
            .Select(g => new
            {
                CustomerName = g.Key.Name,
                LastPm = g.Max(w => w.CompletedAt),
                JobNumber = g.OrderByDescending(w => w.CompletedAt).Select(w => w.JobNumber).FirstOrDefault()
            })
            .ToListAsync();

        var now = DateTime.UtcNow;
        var overduePms = pmCustomers.Where(p => p.LastPm == null || (now - p.LastPm.Value).TotalDays > 180).ToList();

        return Ok(new
        {
            TotalAR = totalAr,
            TotalAP = totalAp,
            NetPosition = totalAr - totalAp,
            AR = ar,
            AP = ap,
            OpenWorkOrders = openWorkOrders,
            BoardColumns = boardColumns,
            OverduePms = overduePms
        });
    }
}