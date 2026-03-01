using System;

namespace PatriotMechanical.API.Domain.Entities
{
    public class CustomerContact
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;

        public long ServiceTitanContactId { get; set; }
        public string Type { get; set; } = null!;   // Phone, MobilePhone, Email, Fax
        public string Value { get; set; } = null!;  // the actual number or address
        public string? Memo { get; set; }            // e.g. "Owner's cell"
        public bool Active { get; set; } = true;
    }
}