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
                // Use InvoiceDate as fallback if IssueDate is unset (MinValue)
                EffectiveDate = i.IssueDate == DateTime.MinValue ? i.InvoiceDate : i.IssueDate
            })
            .ToListAsync();

        var now = DateTime.UtcNow;

        var ar = arRaw
            .GroupBy(i => i.CustomerName)
            .Select(g => new
            {
                Name = g.Key,
                TotalOwed = g.Sum(i => i.BalanceRemaining),
                OldestInvoiceDays = g.Any(i => i.EffectiveDate != DateTime.MinValue)
                    ? (int)(now - g.Where(i => i.EffectiveDate != DateTime.MinValue).Min(i => i.EffectiveDate)).TotalDays
                    : 0
            })
            .OrderByDescending(x => x.TotalOwed)
            .ToList();

        // AR Aging buckets based on EffectiveDate (IssueDate with InvoiceDate fallback)
        var arAging = new
        {
            Bucket0_30   = arRaw.Where(i => i.EffectiveDate != DateTime.MinValue && (now - i.EffectiveDate).TotalDays <= 30).Sum(i => i.BalanceRemaining),
            Bucket31_60  = arRaw.Where(i => i.EffectiveDate != DateTime.MinValue && (now - i.EffectiveDate).TotalDays is > 30 and <= 60).Sum(i => i.BalanceRemaining),
            Bucket61_90  = arRaw.Where(i => i.EffectiveDate != DateTime.MinValue && (now - i.EffectiveDate).TotalDays is > 60 and <= 90).Sum(i => i.BalanceRemaining),
            Bucket90Plus = arRaw.Where(i => i.EffectiveDate != DateTime.MinValue && (now - i.EffectiveDate).TotalDays > 90).Sum(i => i.BalanceRemaining),
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
                    .Select(card => new { card.JobNumber, card.CustomerName, card.AddedAt })
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

        // ── MONTH REVENUE ──────────────────────────────────────────
        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = thisMonthStart.AddMonths(-1);
        var lastMonthEnd   = thisMonthStart;

        var allInvoices = await _context.Invoices
            .Where(i => !isDemo || i.Customer.Name.StartsWith("[DEMO]"))
            .Where(i => isDemo  || !i.Customer.Name.StartsWith("[DEMO]"))
            .Select(i => new
            {
                EffectiveDate = i.IssueDate == DateTime.MinValue ? i.InvoiceDate : i.IssueDate,
                i.TotalAmount
            })
            .ToListAsync();

        var revenueThisMonth = allInvoices
            .Where(i => i.EffectiveDate >= thisMonthStart && i.EffectiveDate < now)
            .Sum(i => i.TotalAmount);

        var revenueLastMonth = allInvoices
            .Where(i => i.EffectiveDate >= lastMonthStart && i.EffectiveDate < lastMonthEnd)
            .Sum(i => i.TotalAmount);

        // ── SCHEDULE STRIP ────────────────────────────────────────
        var todayUtc    = now.Date;
        var tomorrowUtc = todayUtc.AddDays(1);
        var dayAfterUtc = todayUtc.AddDays(2);
        var windowEnd   = todayUtc.AddDays(3);

        var appts = await _context.Appointments
            .Where(a => a.Start >= todayUtc && a.Start < windowEnd)
            .Where(a => a.Status != "Canceled" && a.Status != "Cancelled")
            .Include(a => a.Technicians)
            .Include(a => a.WorkOrder)
                .ThenInclude(w => w == null ? null : w.Customer)
            .ToListAsync();

        // Build appointment items with tech + job info
        object BuildDaySchedule(DateTime dayUtc)
        {
            var dayAppts = appts.Where(a => a.Start.Date == dayUtc).ToList();
            var items = dayAppts.Select(a => new
            {
                JobNumber = a.WorkOrder?.JobNumber ?? "",
                CustomerName = a.WorkOrder?.Customer?.Name ?? "",
                Start = a.Start,
                Techs = a.Technicians.Select(t => t.TechnicianName).ToList()
            }).ToList();
            return new { Count = dayAppts.Count, Items = items };
        }

        var schedToday    = BuildDaySchedule(todayUtc);
        var schedTomorrow = BuildDaySchedule(tomorrowUtc);
        var schedDayAfter = BuildDaySchedule(dayAfterUtc);

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
            OpenWoCount       = openWoCount,
            OverduePmCount    = overduePmCount,
            RevenueThisMonth  = revenueThisMonth,
            RevenueLastMonth  = revenueLastMonth,
            ScheduledToday    = schedToday,
            ScheduledTomorrow = schedTomorrow,
            ScheduledDayAfter = schedDayAfter
        });
    }
}