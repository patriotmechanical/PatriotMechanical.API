using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("pm")]
    public class PmController : ControllerBase
    {
        private readonly AppDbContext _context;
        private static readonly string[] PmKeywords = { "maintenance", "tune up", "tune-up", "pm" };

        public PmController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetPmSummary()
        {
            var isDemo = DemoFilter.IsDemo(User);

            var pmJobs = await _context.WorkOrders
                .Where(w => w.JobTypeName != null &&
                    PmKeywords.Any(k => w.JobTypeName.ToLower().Contains(k)))
                .Where(w => !isDemo || w.Customer.Name.StartsWith("[DEMO]"))
                .Include(w => w.Customer)
                .ToListAsync();

            var grouped = pmJobs
                .GroupBy(w => new { w.CustomerId, w.Customer.Name })
                .Select(g =>
                {
                    var lastJob = g.OrderByDescending(w => w.CompletedAt ?? w.CreatedAt).First();
                    var lastDate = lastJob.CompletedAt ?? lastJob.CreatedAt;
                    var daysSince = (DateTime.UtcNow - lastDate).Days;
                    return new
                    {
                        CustomerId = g.Key.CustomerId,
                        CustomerName = g.Key.Name,
                        LastPmDate = lastDate,
                        LastJobNumber = lastJob.JobNumber,
                        LastJobType = lastJob.JobTypeName,
                        TotalPms = g.Count(),
                        DaysSinceLastPm = daysSince,
                        IsOverdue = daysSince > 365
                    };
                })
                .OrderByDescending(x => x.DaysSinceLastPm)
                .ToList();

            return Ok(grouped);
        }
    }
}