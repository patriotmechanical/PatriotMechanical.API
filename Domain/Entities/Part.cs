using System;

namespace PatriotMechanical.API.Domain.Entities
{
    public class Part
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = null!;

        public decimal UnitCost { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
