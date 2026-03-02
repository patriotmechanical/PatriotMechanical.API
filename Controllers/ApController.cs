using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Infrastructure.Data;
using PatriotMechanical.API.Domain.Entities;

namespace PatriotMechanical.API.Controllers;

[Authorize]
    [ApiController]
[Route("ap")]
public class ApController : ControllerBase
{
    private readonly AppDbContext _context;

    public ApController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("vendors")]
    public async Task<IActionResult> GetVendors()
    {
        var isDemo = DemoFilter.IsDemo(User);
        var vendors = await _context.Vendors
            .Where(v => !isDemo || v.Name.StartsWith("[DEMO]"))
            .Where(v => isDemo || !v.Name.StartsWith("[DEMO]"))
            .Select(v => new
            {
                v.Id,
                v.Name
            })
            .ToListAsync();

        return Ok(vendors);
    }

    [HttpPost("bills")]
public async Task<IActionResult> AddBill([FromBody] ApBill bill)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);

    if (bill.VendorId == Guid.Empty)
        return BadRequest("Vendor is required.");

    bill.Id = Guid.NewGuid();
    bill.DueDate = DateTime.SpecifyKind(bill.DueDate, DateTimeKind.Utc);
    bill.CreatedOn = DateTime.UtcNow;
    bill.IsPaid = false;

    if (bill.TotalAmount <= 0)
        bill.TotalAmount = bill.Amount;

    _context.ApBills.Add(bill);
    await _context.SaveChangesAsync();

    return Ok();
}
    [HttpGet("vendor/{id}")]
public async Task<IActionResult> GetVendorBills(Guid id)
{
    var vendor = await _context.Vendors
        .Include(v => v.Bills)
        .FirstOrDefaultAsync(v => v.Id == id);

    if (vendor == null)
        return NotFound();

    var bills = vendor.Bills
        .Where(b => !b.IsPaid)
        .OrderBy(b => b.DueDate)
        .Select(b => new
        {
            b.Id,
            b.Amount,
            b.TotalAmount,
            b.DueDate,
            b.IsPaid
        });

    return Ok(new
    {
        vendor.Id,
        vendor.Name,
        Bills = bills
    });
}
   [HttpDelete("bills/{id}")]
public async Task<IActionResult> DeleteBill(Guid id)
{
    var bill = await _context.ApBills.FindAsync(id);

    if (bill == null)
        return NotFound();

    _context.ApBills.Remove(bill);
    await _context.SaveChangesAsync();

    return Ok();
}
    [HttpPost("vendors")]
public async Task<IActionResult> AddVendor([FromBody] Vendor vendor)
{
    if (string.IsNullOrWhiteSpace(vendor.Name))
        return BadRequest("Vendor name is required.");

    vendor.Id = Guid.NewGuid();

    _context.Vendors.Add(vendor);
    await _context.SaveChangesAsync();

    return Ok(new { vendor.Id, vendor.Name });
}
    [HttpGet]
public async Task<IActionResult> GetApSummary()
{
    var isDemo = DemoFilter.IsDemo(User);
    var summary = await _context.Vendors
        .Where(v => !isDemo || v.Name.StartsWith("[DEMO]"))
        .Where(v => isDemo || !v.Name.StartsWith("[DEMO]"))
        .Select(v => new
        {
            v.Id,
            v.Name,

            TotalInvoiceAmount = v.Bills
                .Where(b => !b.IsPaid)
                .Sum(b => (decimal?)b.TotalAmount) ?? 0,

            AmountDueNow = v.Bills
                .Where(b => !b.IsPaid)
                .Sum(b => (decimal?)b.Amount) ?? 0,

            NextDue = v.Bills
                .Where(b => !b.IsPaid)
                .OrderBy(b => b.DueDate)
                .Select(b => b.DueDate)
                .FirstOrDefault()
        })
        .OrderByDescending(v => v.AmountDueNow)
        .ToListAsync();

    return Ok(summary);
}

    [HttpPost("bills/fix-totals")]
    public async Task<IActionResult> FixBillTotals()
    {
        var badBills = await _context.ApBills
            .Where(b => b.TotalAmount <= 0)
            .ToListAsync();

        foreach (var bill in badBills)
            bill.TotalAmount = bill.Amount;

        await _context.SaveChangesAsync();

        return Ok(new { fixed_count = badBills.Count });
    }

    [HttpPut("pay/{id}")]
    public async Task<IActionResult> MarkPaid(Guid id)
    {
        var bill = await _context.ApBills.FindAsync(id);
        if (bill == null) return NotFound();

        bill.IsPaid = true;
        await _context.SaveChangesAsync();

        return Ok();
    }
}