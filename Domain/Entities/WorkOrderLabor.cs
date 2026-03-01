using System;

namespace PatriotMechanical.API.Domain.Entities
{
    public class WorkOrderLabor
    {
        public Guid Id { get; set; }

        public Guid WorkOrderId { get; set; }
        public WorkOrder WorkOrder { get; set; } = null!;

        public decimal HoursWorked { get; set; }
        public decimal HourlyCostSnapshot { get; set; }

        public decimal BilledHours { get; set; }
        public decimal BilledRate { get; set; }
    }
}