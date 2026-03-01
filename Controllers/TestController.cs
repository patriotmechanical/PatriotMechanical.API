using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Application.Services;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("test")]
    public class TestController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly JobCostCalculator _calculator;

        public TestController(AppDbContext context, JobCostCalculator calculator)
        {
            _context = context;
            _calculator = calculator;
        }
        [HttpGet("workorders")]
public async Task<IActionResult> GetWorkOrders()
{
    var orders = await _context.WorkOrders
        .Select(w => new { w.Id, w.JobNumber })
        .ToListAsync();

    return Ok(orders);
}
        [HttpGet("parts")]
public async Task<IActionResult> GetParts()
{
    var parts = await _context.Parts
        .Select(p => new { p.Id, p.Name })
        .ToListAsync();

    return Ok(parts);
}

    [HttpGet("job-profit")]
public async Task<IActionResult> GetJobProfit()
{
    var workOrder = await _context.WorkOrders
        .Select(w => new
        {
            w.JobNumber,
            w.TotalLaborCost,
            w.TotalMaterialCost,
            w.TotalRevenueCalculated,
            w.GrossProfit,
            w.NetProfit,
            w.MarginPercent
        })
        .FirstOrDefaultAsync();

    if (workOrder == null)
        return BadRequest("No work orders found.");

    return Ok(workOrder);
}
    }
}