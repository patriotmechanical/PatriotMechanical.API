using System;

namespace PatriotMechanical.API.Domain.Entities
{
    public class LocationContact
    {
        public Guid Id { get; set; }
        public Guid LocationId { get; set; }
        public CustomerLocation Location { get; set; } = null!;

        public long ServiceTitanContactId { get; set; }
        public string Type { get; set; } = null!;
        public string Value { get; set; } = null!;
        public string? Memo { get; set; }
        public bool Active { get; set; } = true;
    }
}