using System;

namespace PatriotMechanical.API.Domain.Entities
{
    public class VendorBill
    {
        public Guid Id { get; set; }

        public Guid VendorId { get; set; }
        public Vendor Vendor { get; set; } = null!;

        public Guid? WorkOrderId { get; set; }
        public WorkOrder? WorkOrder { get; set; }

        public string BillNumber { get; set; } = null!;

        public DateTime IssueDate { get; set; }
        public DateTime DueDate { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal BalanceRemaining { get; set; }

        public string Status { get; set; } = "Open"; // Open, Paid, Partial
    }
}