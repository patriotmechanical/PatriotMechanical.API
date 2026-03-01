using System;
using System.Text.Json.Serialization;

namespace PatriotMechanical.API.Domain.Entities
{
    public class WorkOrderMaterial
    {
        public Guid Id { get; set; }

        public Guid WorkOrderId { get; set; }

        [JsonIgnore]
        public WorkOrder WorkOrder { get; set; } = null!;

        public Guid? PartId { get; set; }

        [JsonIgnore]
        public Part? Part { get; set; }

        public string PartName { get; set; } = null!;

        public decimal Quantity { get; set; }

        public decimal UnitCostSnapshot { get; set; }

        public decimal OriginalCalculatedPrice { get; set; }

        public decimal FinalUnitPrice { get; set; }

        public bool WasPriceOverridden { get; set; } = false;
    }
}