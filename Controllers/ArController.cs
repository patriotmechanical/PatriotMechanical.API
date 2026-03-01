using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("ar")]
    public class ArController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ArController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var invoices = await _context.Invoices
                .Include(i => i.Payments)
                .ToListAsync();

            var totalOutstanding = invoices.Sum(i => i.BalanceRemaining);
            var openInvoices = invoices.Count(i => i.Status != "Paid");

            return Ok(new
            {
                totalOutstanding,
                openInvoices
            });
        }

        [HttpGet("aging")]
public async Task<IActionResult> GetAging()
{
    var today = DateTime.UtcNow;

    var invoices = await _context.Invoices.ToListAsync();

    var aging = new
    {
        current = invoices
            .Where(i => (today - i.IssueDate).Days <= 30)
            .Sum(i => i.BalanceRemaining),

        days31to60 = invoices
            .Where(i => (today - i.IssueDate).Days > 30 &&
                        (today - i.IssueDate).Days <= 60)
            .Sum(i => i.BalanceRemaining),

        days61to90 = invoices
            .Where(i => (today - i.IssueDate).Days > 60 &&
                        (today - i.IssueDate).Days <= 90)
            .Sum(i => i.BalanceRemaining),

        over90 = invoices
            .Where(i => (today - i.IssueDate).Days > 90)
            .Sum(i => i.BalanceRemaining)
    };

    return Ok(aging);
}
    }
}