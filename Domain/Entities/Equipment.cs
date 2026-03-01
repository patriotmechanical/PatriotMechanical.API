using System;

namespace PatriotMechanical.API.Domain.Entities
{
    public class Equipment
    {
        public Guid Id { get; set; }

        public Guid CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        public Guid? WorkOrderId { get; set; }
        public WorkOrder? WorkOrder { get; set; }

        public string Type { get; set; } = null!;        // AC/Condenser, Furnace, etc.
        public string? Brand { get; set; }
        public string? ModelNumber { get; set; }
        public string? SerialNumber { get; set; }
        public DateTime? InstallDate { get; set; }
        public DateTime? WarrantyExpiration { get; set; }
        public string? Notes { get; set; }

        public bool WarrantyRegistered { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}