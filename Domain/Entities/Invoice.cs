using System;
using System.Collections.Generic;

namespace PatriotMechanical.API.Domain.Entities
{
    public class Invoice
    {
        public Guid Id { get; set; }

        // Existing Fields (KEEP THESE)
        public string InvoiceNumber { get; set; } = null!;
        public DateTime IssueDate { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal BalanceRemaining { get; set; }

        public string? Status { get; set; }

        // Relationships
        public Guid CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        public Guid? WorkOrderId { get; set; }
        public WorkOrder WorkOrder { get; set; } = null!;

        public ICollection<Payment> Payments { get; set; } = new List<Payment>();

        // NEW — ServiceTitan Mirror Fields
        public long ServiceTitanInvoiceId { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime LastSyncedFromServiceTitan { get; set; }
    }
}