namespace PatriotMechanical.API.Domain.Entities
{
    public class WarrantyClaim
    {
        public Guid Id { get; set; }

        // Part info
        public string PartName { get; set; } = null!;
        public string? PartModelNumber { get; set; }
        public string? PartSerialNumber { get; set; }

        // Unit info (the equipment the part came from)
        public string? UnitModelNumber { get; set; }
        public string? UnitSerialNumber { get; set; }

        // Links
        public Guid? CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public string? CustomerName { get; set; } // denormalized for quick display
        public string? JobNumber { get; set; } // original job where part failed
        public string? ReturnJobNumber { get; set; } // job for the warranty repair

        // Supplier / manufacturer
        public string? Supplier { get; set; } // distributor (Johnstone, United, etc.)
        public string? Manufacturer { get; set; }

        // Claim details
        public string? RmaNumber { get; set; }
        public string Status { get; set; } = "Diagnosis"; // Diagnosis, Claim Filed, Approved, Part Ordered, Part Shipped, Part Received, Installed, Defective Returned, Closed
        public string ClaimType { get; set; } = "Replacement"; // Replacement or Credit
        public decimal? CreditAmount { get; set; } // if ClaimType == Credit

        // Dates
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ClaimFiledDate { get; set; }
        public DateTime? ApprovedDate { get; set; }
        public DateTime? ExpectedShipDate { get; set; }
        public DateTime? PartReceivedDate { get; set; }
        public DateTime? InstalledDate { get; set; }
        public DateTime? DefectiveReturnedDate { get; set; }
        public DateTime? ClosedDate { get; set; }

        // Flags
        public bool DefectivePartReturned { get; set; } = false;
        public bool IsClosed { get; set; } = false;

        // Notes
        public List<WarrantyClaimNote> Notes { get; set; } = new();
    }
}