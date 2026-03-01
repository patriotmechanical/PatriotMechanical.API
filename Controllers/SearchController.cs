using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("search")]
    public class SearchController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SearchController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Ok(new { results = Array.Empty<object>() });

            var term = q.Trim().ToLower();
            var isDemo = DemoFilter.IsDemo(User);
            var results = new List<object>();

            // Customers
            var customers = await _context.Customers
                .Where(c => !isDemo || c.Name.StartsWith("[DEMO]"))
                .Where(c => isDemo || !c.Name.StartsWith("[DEMO]"))
                .Where(c => c.Name.ToLower().Contains(term))
                .Take(5)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();

            foreach (var c in customers)
                results.Add(new { type = "customer", id = c.Id, title = c.Name, subtitle = "Customer" });

            // Work Orders (by job number or customer name)
            var workOrders = await _context.WorkOrders
                .Include(w => w.Customer)
                .Where(w => !isDemo || (w.Customer != null && w.Customer.Name.StartsWith("[DEMO]")))
                .Where(w => isDemo || w.Customer == null || !w.Customer.Name.StartsWith("[DEMO]"))
                .Where(w => (w.JobNumber != null && w.JobNumber.ToLower().Contains(term))
                         || (w.Customer != null && w.Customer.Name.ToLower().Contains(term)))
                .Take(5)
                .Select(w => new { w.Id, w.JobNumber, CustomerName = w.Customer != null ? w.Customer.Name : "Unknown", w.Status })
                .ToListAsync();

            foreach (var w in workOrders)
                results.Add(new { type = "workorder", id = w.Id, title = "Job #" + w.JobNumber, subtitle = w.CustomerName + " — " + w.Status });

            // Equipment (by serial, model, brand, or customer)
            var equipment = await _context.Equipment
                .Include(e => e.Customer)
                .Where(e => !isDemo || (e.Customer != null && e.Customer.Name.StartsWith("[DEMO]")))
                .Where(e => isDemo || e.Customer == null || !e.Customer.Name.StartsWith("[DEMO]"))
                .Where(e => (e.SerialNumber != null && e.SerialNumber.ToLower().Contains(term))
                         || (e.ModelNumber != null && e.ModelNumber.ToLower().Contains(term))
                         || (e.Brand != null && e.Brand.ToLower().Contains(term))
                         || (e.EquipmentType != null && e.EquipmentType.ToLower().Contains(term)))
                .Take(5)
                .Select(e => new { e.Id, e.EquipmentType, e.Brand, e.SerialNumber, CustomerName = e.Customer != null ? e.Customer.Name : "" })
                .ToListAsync();

            foreach (var e in equipment)
                results.Add(new { type = "equipment", id = e.Id, title = (e.Brand + " " + e.EquipmentType).Trim(), subtitle = "S/N: " + (e.SerialNumber ?? "—") + " — " + e.CustomerName });

            // Vendors
            var vendors = await _context.Vendors
                .Where(v => !isDemo || v.Name.StartsWith("[DEMO]"))
                .Where(v => isDemo || !v.Name.StartsWith("[DEMO]"))
                .Where(v => v.Name.ToLower().Contains(term))
                .Take(5)
                .Select(v => new { v.Id, v.Name })
                .ToListAsync();

            foreach (var v in vendors)
                results.Add(new { type = "vendor", id = v.Id, title = v.Name, subtitle = "Vendor" });

            // Warranty Claims (by part name, RMA, customer, or job number)
            var warranties = await _context.WarrantyClaims
                .Where(w => !isDemo || w.IsDemo)
                .Where(w => isDemo || !w.IsDemo)
                .Where(w => (w.PartName.ToLower().Contains(term))
                         || (w.RmaNumber != null && w.RmaNumber.ToLower().Contains(term))
                         || (w.CustomerName != null && w.CustomerName.ToLower().Contains(term))
                         || (w.JobNumber != null && w.JobNumber.ToLower().Contains(term)))
                .Take(5)
                .Select(w => new { w.Id, w.PartName, w.CustomerName, w.Status })
                .ToListAsync();

            foreach (var w in warranties)
                results.Add(new { type = "warranty", id = w.Id, title = w.PartName, subtitle = "Warranty — " + w.Status + (w.CustomerName != null ? " — " + w.CustomerName : "") });

            // Subcontractors
            var subs = await _context.Subcontractors
                .Where(s => !isDemo || s.Name.StartsWith("[DEMO]"))
                .Where(s => isDemo || !s.Name.StartsWith("[DEMO]"))
                .Where(s => s.Name.ToLower().Contains(term)
                         || (s.Company != null && s.Company.ToLower().Contains(term))
                         || (s.Trade != null && s.Trade.ToLower().Contains(term)))
                .Take(5)
                .Select(s => new { s.Id, s.Name, s.Company, s.Trade })
                .ToListAsync();

            foreach (var s in subs)
                results.Add(new { type = "subcontractor", id = s.Id, title = s.Name, subtitle = (s.Trade ?? "") + " — " + (s.Company ?? "") });

            return Ok(new { results });
        }
    }
}