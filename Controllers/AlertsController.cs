using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Domain.Entities;
using PatriotMechanical.API.Infrastructure.Data;
using System.Security.Claims;

namespace PatriotMechanical.API.Controllers;

[ApiController]
[Route("alerts")]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AlertsController(AppDbContext context)
    {
        _context = context;
    }

    // ─── GET AR ALERTS ────────────────────────────────────────────
    [HttpGet("ar")]
    public async Task<IActionResult> GetArAlerts()
    {
        var companyId = GetCompanyId();
        var company = await _context.CompanySettings.FindAsync(companyId);
        if (company == null) return NotFound();

        // Get all invoices with outstanding balances
        var invoices = await _context.Invoices
            .Where(i => i.BalanceRemaining > 0)
            .Where(i => !i.Customer.Name.StartsWith("[DEMO]"))
            .Select(i => new
            {
                i.CustomerId,
                CustomerName = i.Customer.Name,
                i.BalanceRemaining,
                EffectiveDate = i.IssueDate == DateTime.MinValue ? i.InvoiceDate : i.IssueDate
            })
            .ToListAsync();

        var now = DateTime.UtcNow;

        // Group by customer and compute aging
        var customerAr = invoices
            .GroupBy(i => i.CustomerId)
            .Select(g =>
            {
                var totalBalance = g.Sum(i => i.BalanceRemaining);
                var oldest = g.Where(i => i.EffectiveDate != DateTime.MinValue)
                              .Select(i => (now - i.EffectiveDate).TotalDays)
                              .DefaultIfEmpty(0)
                              .Max();
                oldest = oldest > 3650 ? 0 : oldest; // filter out bad dates

                var days30 = g.Where(i => i.EffectiveDate != DateTime.MinValue
                    && (now - i.EffectiveDate).TotalDays is > 30 and <= 60)
                    .Sum(i => i.BalanceRemaining);
                var days60 = g.Where(i => i.EffectiveDate != DateTime.MinValue
                    && (now - i.EffectiveDate).TotalDays is > 60 and <= 90)
                    .Sum(i => i.BalanceRemaining);
                var days90 = g.Where(i => i.EffectiveDate != DateTime.MinValue
                    && (now - i.EffectiveDate).TotalDays > 90)
                    .Sum(i => i.BalanceRemaining);

                return new
                {
                    CustomerId = g.Key,
                    CustomerName = g.First().CustomerName,
                    TotalBalance = totalBalance,
                    OldestDays = (int)oldest,
                    Days30 = days30,
                    Days60 = days60,
                    Days90 = days90
                };
            })
            .ToList();

        // Get dismissals for this company
        var dismissed = await _context.ArAlertDismissals
            .Where(d => d.CompanySettingsId == companyId)
            .Select(d => d.CustomerId)
            .ToListAsync();

        // Evaluate each customer against thresholds
        var alerts = customerAr
            .Select(c =>
            {
                var reasons = new List<string>();

                if (company.ArAlertOnBalanceAmount && c.TotalBalance >= company.ArAlertBalanceThreshold)
                    reasons.Add($"Balance ${c.TotalBalance:N2} exceeds ${company.ArAlertBalanceThreshold:N0} threshold");

                if (company.ArAlertOn30Days && c.Days30 > company.ArAlertDays30Threshold)
                    reasons.Add($"${c.Days30:N2} is 31–60 days past due");

                if (company.ArAlertOn60Days && c.Days60 > company.ArAlertDays60Threshold)
                    reasons.Add($"${c.Days60:N2} is 61–90 days past due");

                if (company.ArAlertOn90Days && c.Days90 > company.ArAlertDays90Threshold)
                    reasons.Add($"${c.Days90:N2} is 90+ days past due");

                return new
                {
                    c.CustomerId,
                    c.CustomerName,
                    c.TotalBalance,
                    c.OldestDays,
                    c.Days30,
                    c.Days60,
                    c.Days90,
                    Reasons = reasons,
                    IsTriggered = reasons.Count > 0,
                    IsDismissed = dismissed.Contains(c.CustomerId)
                };
            })
            .Where(a => a.IsTriggered)
            .OrderByDescending(a => a.TotalBalance)
            .ToList();

        return Ok(new
        {
            alerts,
            totalTriggered = alerts.Count,
            activeAlerts = alerts.Count(a => !a.IsDismissed)
        });
    }

    // ─── DISMISS AN ALERT ─────────────────────────────────────────
    [HttpPost("ar/{customerId}/dismiss")]
    public async Task<IActionResult> DismissAlert(Guid customerId)
    {
        var companyId = GetCompanyId();
        var userName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";

        var existing = await _context.ArAlertDismissals
            .FirstOrDefaultAsync(d => d.CompanySettingsId == companyId && d.CustomerId == customerId);

        if (existing != null)
            return Ok(new { message = "Already dismissed." });

        _context.ArAlertDismissals.Add(new ArAlertDismissal
        {
            CompanySettingsId = companyId,
            CustomerId = customerId,
            DismissedAt = DateTime.UtcNow,
            DismissedBy = userName
        });

        await _context.SaveChangesAsync();
        return Ok(new { message = "Alert dismissed." });
    }

    // ─── UN-DISMISS AN ALERT ──────────────────────────────────────
    [HttpDelete("ar/{customerId}/dismiss")]
    public async Task<IActionResult> UndismissAlert(Guid customerId)
    {
        var companyId = GetCompanyId();

        var existing = await _context.ArAlertDismissals
            .FirstOrDefaultAsync(d => d.CompanySettingsId == companyId && d.CustomerId == customerId);

        if (existing == null)
            return Ok(new { message = "Not dismissed." });

        _context.ArAlertDismissals.Remove(existing);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Dismissal removed." });
    }

    private Guid GetCompanyId()
    {
        var claim = User.FindFirst("companyId")?.Value;
        return Guid.Parse(claim!);
    }
}