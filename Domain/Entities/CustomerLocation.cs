using System;
using System.Collections.Generic;

namespace PatriotMechanical.API.Domain.Entities
{
    public class CustomerLocation
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        public long ServiceTitanLocationId { get; set; }
        public string Name { get; set; } = null!;
        public string? Street { get; set; }
        public string? Unit { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Zip { get; set; }
        public bool Active { get; set; } = true;

        public ICollection<LocationContact> Contacts { get; set; } = new List<LocationContact>();
    }
}