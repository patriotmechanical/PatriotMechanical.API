using PatriotMechanical.API.Domain.Entities;

namespace PatriotMechanical.API.Application.Services
{
    public class JobCostCalculator
    {
        private const decimal CreditCardFeePercent = 0.025m;

        public decimal CalculateLaborCost(WorkOrder workOrder)
        {
            return workOrder.LaborEntries
                .Sum(l => l.HoursWorked * l.HourlyCostSnapshot);
        }

        public decimal CalculateMaterialCost(WorkOrder workOrder)
        {
            return workOrder.MaterialEntries
                .Sum(m => m.Quantity * m.UnitCostSnapshot);
        }

        public decimal CalculateGrossProfit(WorkOrder workOrder)
        {
            var laborCost = CalculateLaborCost(workOrder);
            var materialCost = CalculateMaterialCost(workOrder);

            return workOrder.TotalAmount - (laborCost + materialCost);
        }

        public decimal CalculateNetProfitAfterCCFee(WorkOrder workOrder)
        {
            var grossProfit = CalculateGrossProfit(workOrder);
            var ccFee = workOrder.TotalAmount * CreditCardFeePercent;

            return grossProfit - ccFee;
        }

        public decimal CalculateProfitMarginPercent(WorkOrder workOrder)
        {
            if (workOrder.TotalAmount == 0)
                return 0;

            var netProfit = CalculateNetProfitAfterCCFee(workOrder);
            return (netProfit / workOrder.TotalAmount) * 100;
        }
    }
}