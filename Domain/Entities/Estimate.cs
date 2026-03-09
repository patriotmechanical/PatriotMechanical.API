using System;
using System.Collections.Generic;

namespace PatriotMechanical.API.Domain.Entities
{
    public class Estimate
    {
        public Guid Id { get; set; }

        // ServiceTitan mirror fields
        public long ServiceTitanEstimateId { get; set; }
        public long ServiceTitanJobId { get; set; }
        public long ServiceTitanCustomerId { get; set; }
        public string JobNumber { get; set; } = "";
        public string EstimateName { get; set; } = "";
        public string Status { get; set; } = ""; // Open, Sold, Dismissed
        public string ReviewStatus { get; set; } = ""; // None, NeedsApproval, Approved, NotApproved
        public string Summary { get; set; } = "";
        public string BusinessUnitName { get; set; } = "";
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public DateTime? SoldOn { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime LastSyncedFromServiceTitan { get; set; }

        // Local customer link (nullable — not all estimates will match a synced customer)
        public Guid? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        // Follow-up data (managed in MyOpsBoard, not synced from ST)
        public EstimateFollowUp? FollowUp { get; set; }
    }

    public class EstimateFollowUp
    {
        public Guid Id { get; set; }
        public Guid EstimateId { get; set; }
        public Estimate Estimate { get; set; } = null!;

        public DateTime? FollowUpDate { get; set; }
        public string AssignedTo { get; set; } = "";
        public string Outcome { get; set; } = "Pending"; // Pending, Won, Lost
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Call attempt log (timestamped notes)
        public ICollection<EstimateFollowUpNote> Notes { get; set; } = new List<EstimateFollowUpNote>();
    }

    public class EstimateFollowUpNote
    {
        public Guid Id { get; set; }
        public Guid FollowUpId { get; set; }
        public EstimateFollowUp FollowUp { get; set; } = null!;

        public string Text { get; set; } = "";
        public string Author { get; set; } = "";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}