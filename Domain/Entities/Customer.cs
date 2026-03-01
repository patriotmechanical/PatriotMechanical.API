using System;
using System.Collections.Generic;

namespace PatriotMechanical.API.Domain.Entities
{
    public class Customer
    {
        public Guid Id { get; set; }

        // 🔗 Bridge to ServiceTitan
        public long ServiceTitanCustomerId { get; set; }
        public DateTime? ServiceTitanModifiedOn { get; set; }

        public string Name { get; set; } = null!;
        public string? Phone { get; set; }
        public string? Email { get; set; }

        public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

        public DateTime LastSyncedFromServiceTitan { get; set; }

        public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
    }
}