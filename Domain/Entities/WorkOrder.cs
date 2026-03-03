using System;
using System.Collections.Generic;

namespace PatriotMechanical.API.Domain.Entities
{
    public class WorkOrder
    {
        public Guid Id { get; set; }
        
        public string JobNumber { get; set; } = null!;
        public string Status { get; set; } = "Open";
        public string? JobTypeName { get; set; }

        public Guid CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        public long ServiceTitanJobId { get; set; }
        public DateTime? ServiceTitanModifiedOn { get; set; }
        public long ServiceTitanCustomerId { get; set; }
        public long ServiceTitanLocationId { get; set; }
        public Guid? TechnicianId { get; set; }
        public Technician? Technician { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal TotalAmount { get; set; }
        
        public DateTime? LastSyncedFromServiceTitan { get; set; }

        public ICollection<WorkOrderLabor> LaborEntries { get; set; } = new List<WorkOrderLabor>();
        public ICollection<WorkOrderMaterial> MaterialEntries { get; set; } = new List<WorkOrderMaterial>();

        public Invoice? Invoice { get; set; }

        public decimal TotalLaborCost { get; set; }
        public decimal TotalMaterialCost { get; set; }
        public decimal TotalRevenueCalculated { get; set; }

        public decimal GrossProfit { get; set; }
        public decimal NetProfit { get; set; }
        public decimal MarginPercent { get; set; }
    }
}