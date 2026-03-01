using System;
using System.Collections.Generic;

namespace PatriotMechanical.API.Domain.Entities
{
    public class Subcontractor
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Company { get; set; }
        public string? Trade { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<SubcontractorEntry> Entries { get; set; } = new List<SubcontractorEntry>();
    }
}