using System;

namespace PatriotMechanical.API.Domain.Entities
{
    public class SubcontractorEntry
    {
        public Guid Id { get; set; }

        public Guid SubcontractorId { get; set; }
        public Subcontractor Subcontractor { get; set; } = null!;

        public Guid WorkOrderId { get; set; }
        public WorkOrder WorkOrder { get; set; } = null!;

        public DateTime Date { get; set; }
        public decimal Hours { get; set; }
        public decimal HourlyRate { get; set; }
        public decimal TotalCost => Hours * HourlyRate;

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}