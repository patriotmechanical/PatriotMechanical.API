using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PatriotMechanical.API.Application.Services;
using PatriotMechanical.API.Domain.Entities;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("workorder-materials")]
    public class WorkOrderMaterialsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly PricingEngine _pricing;
        private readonly JobCostingService _jobCosting;

        public WorkOrderMaterialsController(
            AppDbContext context,
            PricingEngine pricing,
            JobCostingService jobCosting)
        {
            _context = context;
            _pricing = pricing;
            _jobCosting = jobCosting;
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddMaterial(Guid workOrderId, Guid partId, decimal quantity)
        {
            var workOrder = await _context.WorkOrders.FindAsync(workOrderId);
            if (workOrder == null)
                return BadRequest("WorkOrder not found.");

            var part = await _context.Parts.FindAsync(partId);
            if (part == null)
                return BadRequest("Part not found.");

            var calculatedPrice = _pricing.CalculateFinalPrice(part.UnitCost);

            var material = new WorkOrderMaterial
            {
                WorkOrderId = workOrderId,
                PartId = part.Id,
                PartName = part.Name,
                Quantity = quantity,
                UnitCostSnapshot = part.UnitCost,
                OriginalCalculatedPrice = calculatedPrice,
                FinalUnitPrice = calculatedPrice,
                WasPriceOverridden = false
            };

            _context.WorkOrderMaterials.Add(material);
            await _context.SaveChangesAsync();

            // 🔥 Recalculate job profitability
            await _jobCosting.RecalculateAsync(workOrderId);

            return Ok(new
            {
                material.Id,
                material.WorkOrderId,
                material.PartId,
                material.PartName,
                material.Quantity,
                material.UnitCostSnapshot,
                material.OriginalCalculatedPrice,
                material.FinalUnitPrice,
                material.WasPriceOverridden
            });
        }
    }
}