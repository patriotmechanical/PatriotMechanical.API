using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Domain.Entities;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("equipment")]
    public class EquipmentController : ControllerBase
    {
        private readonly AppDbContext _context;

        public EquipmentController(AppDbContext context)
        {
            _context = context;
        }

        // GET /equipment — all equipment in the field
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var isDemo = DemoFilter.IsDemo(User);
            var equipment = await _context.Equipment
                .Include(e => e.Customer)
                .Include(e => e.WorkOrder)
                .Where(e => !isDemo || (e.Customer != null && e.Customer.Name.StartsWith("[DEMO]")))
                .Where(e => isDemo || e.Customer == null || !e.Customer.Name.StartsWith("[DEMO]"))
                .OrderByDescending(e => e.InstallDate)
                .Select(e => new
                {
                    e.Id,
                    e.Type,
                    e.Brand,
                    e.ModelNumber,
                    e.SerialNumber,
                    e.InstallDate,
                    e.WarrantyExpiration,
                    e.WarrantyRegistered,
                    e.Notes,
                    CustomerName = e.Customer.Name,
                    CustomerId = e.CustomerId,
                    JobNumber = e.WorkOrder != null ? e.WorkOrder.JobNumber : null,
                    WorkOrderId = e.WorkOrderId
                })
                .ToListAsync();

            return Ok(equipment);
        }

        // GET /equipment/customer/{customerId}
        [HttpGet("customer/{customerId}")]
        public async Task<IActionResult> GetByCustomer(Guid customerId)
        {
            var equipment = await _context.Equipment
                .Where(e => e.CustomerId == customerId)
                .Include(e => e.WorkOrder)
                .OrderByDescending(e => e.InstallDate)
                .Select(e => new
                {
                    e.Id,
                    e.Type,
                    e.Brand,
                    e.ModelNumber,
                    e.SerialNumber,
                    e.InstallDate,
                    e.WarrantyExpiration,
                    e.WarrantyRegistered,
                    e.Notes,
                    JobNumber = e.WorkOrder != null ? e.WorkOrder.JobNumber : null,
                    WorkOrderId = e.WorkOrderId
                })
                .ToListAsync();

            return Ok(equipment);
        }

        // GET /equipment/job/{workOrderId}
        [HttpGet("job/{workOrderId}")]
        public async Task<IActionResult> GetByJob(Guid workOrderId)
        {
            var equipment = await _context.Equipment
                .Where(e => e.WorkOrderId == workOrderId)
                .Include(e => e.Customer)
                .OrderByDescending(e => e.InstallDate)
                .Select(e => new
                {
                    e.Id,
                    e.Type,
                    e.Brand,
                    e.ModelNumber,
                    e.SerialNumber,
                    e.InstallDate,
                    e.WarrantyExpiration,
                    e.WarrantyRegistered,
                    e.Notes,
                    CustomerName = e.Customer.Name
                })
                .ToListAsync();

            return Ok(equipment);
        }

        // POST /equipment
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Equipment equipment)
        {
            if (equipment.CustomerId == Guid.Empty)
                return BadRequest("Customer is required.");
            if (string.IsNullOrWhiteSpace(equipment.Type))
                return BadRequest("Equipment type is required.");

            var customerExists = await _context.Customers.AnyAsync(c => c.Id == equipment.CustomerId);
            if (!customerExists) return BadRequest("Customer not found.");

            equipment.Id = Guid.NewGuid();
            equipment.CreatedAt = DateTime.UtcNow;

            if (equipment.InstallDate.HasValue)
                equipment.InstallDate = DateTime.SpecifyKind(equipment.InstallDate.Value, DateTimeKind.Utc);
            if (equipment.WarrantyExpiration.HasValue)
                equipment.WarrantyExpiration = DateTime.SpecifyKind(equipment.WarrantyExpiration.Value, DateTimeKind.Utc);

            _context.Equipment.Add(equipment);
            await _context.SaveChangesAsync();

            return Ok(new { equipment.Id, equipment.Type, equipment.Brand, equipment.ModelNumber });
        }

        // PUT /equipment/{id}/warranty-registered
        [HttpPut("{id}/warranty-registered")]
        public async Task<IActionResult> ToggleWarrantyRegistered(Guid id)
        {
            var equipment = await _context.Equipment.FindAsync(id);
            if (equipment == null) return NotFound();

            equipment.WarrantyRegistered = !equipment.WarrantyRegistered;
            await _context.SaveChangesAsync();

            return Ok(new { equipment.Id, equipment.WarrantyRegistered });
        }

        // DELETE /equipment/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var equipment = await _context.Equipment.FindAsync(id);
            if (equipment == null) return NotFound();

            _context.Equipment.Remove(equipment);
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}