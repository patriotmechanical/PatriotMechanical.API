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

        var arRaw = await _context.Invoices
            .Where(i => i.BalanceRemaining > 0)
            .Where(i => !isDemo || i.Customer.Name.StartsWith("[DEMO]"))
            .Where(i => isDemo || !i.Customer.Name.StartsWith("[DEMO]"))
            .Select(i => new
            {
                CustomerName = i.Customer.Name,
                i.BalanceRemaining,
                i.IssueDate
            })
            .ToListAsync();

        var ar = arRaw
            .GroupBy(i => i.CustomerName)
            .Select(g => new
            {
                Name = g.Key,
                TotalOwed = g.Sum(i => i.BalanceRemaining)
            })
            .OrderByDescending(x => x.TotalOwed)
            .ToList();

        var now = DateTime.UtcNow;

        // AR Aging buckets based on IssueDate
        var arAging = new
        {
            Bucket0_30   = arRaw.Where(i => (now - i.IssueDate).TotalDays <= 30).Sum(i => i.BalanceRemaining),
            Bucket31_60  = arRaw.Where(i => (now - i.IssueDate).TotalDays is > 30 and <= 60).Sum(i => i.BalanceRemaining),
            Bucket61_90  = arRaw.Where(i => (now - i.IssueDate).TotalDays is > 60 and <= 90).Sum(i => i.BalanceRemaining),
            Bucket90Plus = arRaw.Where(i => (now - i.IssueDate).TotalDays > 90).Sum(i => i.BalanceRemaining),
        };

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

        var overduePms = pmCustomers.Where(p => p.LastPm == null || (now - p.LastPm.Value).TotalDays > 180).ToList();

        // Sidebar badge counts
        var openWoCount    = openWorkOrders.Count;
        var overduePmCount = overduePms.Count;

        return Ok(new
        {
            TotalAR = totalAr,
            TotalAP = totalAp,
            NetPosition = totalAr - totalAp,
            AR = ar,
            ARaging = arAging,
            AP = ap,
            OpenWorkOrders = openWorkOrders,
            BoardColumns = boardColumns,
            OverduePms = overduePms,
            OpenWoCount    = openWoCount,
            OverduePmCount = overduePmCount
        });
    }
}