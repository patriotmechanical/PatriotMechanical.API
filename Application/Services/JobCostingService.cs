using Microsoft.EntityFrameworkCore;
using PatriotMechanical.API.Infrastructure.Data;

namespace PatriotMechanical.API.Application.Services
{
    public class JobCostingService
    {
        private readonly AppDbContext _context;

        public JobCostingService(AppDbContext context)
        {
            _context = context;
        }

        public async Task RecalculateAsync(Guid workOrderId)
{
    var workOrder = await _context.WorkOrders
        .Include(w => w.LaborEntries)
        .Include(w => w.MaterialEntries)
        .FirstOrDefaultAsync(w => w.Id == workOrderId);

    if (workOrder == null) return;

    var laborCost = workOrder.LaborEntries
        .Sum(l => l.HoursWorked * l.HourlyCostSnapshot);

    var laborRevenue = workOrder.LaborEntries
        .Sum(l => l.BilledHours * l.BilledRate);

    var materialCost = workOrder.MaterialEntries
        .Sum(m => m.Quantity * m.UnitCostSnapshot);

    var materialRevenue = workOrder.MaterialEntries
        .Sum(m => m.Quantity * m.FinalUnitPrice);

    var totalCost = laborCost + materialCost;
    var totalRevenue = laborRevenue + materialRevenue;
    var grossProfit = totalRevenue - totalCost;

    // 🔥 Persist EVERYTHING
    workOrder.TotalLaborCost = laborCost;
    workOrder.TotalMaterialCost = materialCost;
    workOrder.TotalRevenueCalculated = totalRevenue;
    workOrder.GrossProfit = grossProfit;
    workOrder.NetProfit = grossProfit;

    workOrder.MarginPercent = totalRevenue == 0
        ? 0
        : Math.Round((grossProfit / totalRevenue) * 100, 2);

    await _context.SaveChangesAsync();
}
    }
}