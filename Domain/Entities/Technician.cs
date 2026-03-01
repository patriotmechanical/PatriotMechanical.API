using System;
using System.Collections.Generic;

namespace PatriotMechanical.API.Domain.Entities
{
    public class Technician
    {
        public Guid Id { get; set; }

        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;

        public decimal HourlyCost { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
    }
}