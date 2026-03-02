using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("customers")]
    public class CustomerController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CustomerController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var today = DateTime.UtcNow;
            var isDemo = DemoFilter.IsDemo(User);

            var customers = await _context.Customers
                .Where(c => !isDemo || c.Name.StartsWith("[DEMO]"))
                .Where(c => isDemo || !c.Name.StartsWith("[DEMO]"))
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    c.ServiceTitanCustomerId,
                    Invoices = c.Invoices.Where(i => i.BalanceRemaining > 0).Select(i => new
                    {
                        i.BalanceRemaining,
                        i.DueDate
                    }).ToList()
                })
                .ToListAsync();

            var result = customers.Select(c =>
            {
                var current = c.Invoices.Where(i => (today - i.DueDate).Days <= 0).Sum(i => i.BalanceRemaining);
                var days30  = c.Invoices.Where(i => (today - i.DueDate).Days > 0  && (today - i.DueDate).Days <= 30).Sum(i => i.BalanceRemaining);
                var days60  = c.Invoices.Where(i => (today - i.DueDate).Days > 30 && (today - i.DueDate).Days <= 60).Sum(i => i.BalanceRemaining);
                var days90  = c.Invoices.Where(i => (today - i.DueDate).Days > 60).Sum(i => i.BalanceRemaining);
                var totalAR = current + days30 + days60 + days90;

                return new
                {
                    c.Id,
                    c.Name,
                    c.ServiceTitanCustomerId,
                    totalAR,
                    current,
                    days30,
                    days60,
                    days90
                };
            });

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetProfile(Guid id)
        {
            var customer = await _context.Customers
                .Where(c => c.Id == id)
                .FirstOrDefaultAsync();

            if (customer == null) return NotFound();

            var contacts = await _context.CustomerContacts
                .Where(c => c.CustomerId == id && c.Active)
                .Select(c => new { c.Type, c.Value, c.Memo })
                .ToListAsync();

            var locations = await _context.CustomerLocations
                .Where(l => l.CustomerId == id && l.Active)
                .Include(l => l.Contacts)
                .Select(l => new
                {
                    l.Name, l.Street, l.Unit, l.City, l.State, l.Zip,
                    Contacts = l.Contacts.Where(c => c.Active).Select(c => new { c.Type, c.Value, c.Memo }).ToList()
                })
                .ToListAsync();

            var invoices = await _context.Invoices
                .Where(i => i.CustomerId == id)
                .OrderByDescending(i => i.InvoiceDate)
                .Select(i => new { i.InvoiceNumber, i.InvoiceDate, i.DueDate, i.TotalAmount, i.BalanceRemaining, i.Status })
                .ToListAsync();

            var workOrders = await _context.WorkOrders
                .Where(w => w.CustomerId == id)
                .OrderByDescending(w => w.CreatedAt)
                .Take(10)
                .Select(w => new { w.JobNumber, w.Status, w.JobTypeName, w.CreatedAt, w.CompletedAt, w.TotalAmount })
                .ToListAsync();

            var equipment = await _context.Equipment
                .Where(e => e.CustomerId == id)
                .OrderByDescending(e => e.InstallDate)
                .Select(e => new { e.Type, e.Brand, e.ModelNumber, e.SerialNumber, e.InstallDate, e.WarrantyExpiration, e.WarrantyRegistered, e.Notes })
                .ToListAsync();

            var pmKeywords = new[] { "maintenance", "tune up", "tune-up", "pm" };
            var lastPm = await _context.WorkOrders
                .Where(w => w.CustomerId == id && w.JobTypeName != null &&
                    pmKeywords.Any(k => w.JobTypeName.ToLower().Contains(k)) && w.CompletedAt != null)
                .OrderByDescending(w => w.CompletedAt)
                .Select(w => new { w.JobNumber, w.JobTypeName, w.CompletedAt })
                .FirstOrDefaultAsync();

            var balanceOwed = await _context.Invoices
                .Where(i => i.CustomerId == id && i.BalanceRemaining > 0)
                .SumAsync(i => i.BalanceRemaining);

            return Ok(new
            {
                customer.Id, customer.Name, customer.ServiceTitanCustomerId,
                BillingStreet = (string?)null,
                Contacts = contacts, Locations = locations, Invoices = invoices,
                WorkOrders = workOrders, Equipment = equipment,
                LastPm = lastPm, BalanceOwed = balanceOwed
            });
        }
    }
}