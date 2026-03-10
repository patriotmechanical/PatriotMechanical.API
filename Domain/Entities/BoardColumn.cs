namespace PatriotMechanical.API.Domain.Entities
{
    public class BoardColumn
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public int SortOrder { get; set; }
        public string Color { get; set; } = "#334155";
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Auto-placement role for ServiceTitan appointment sync.
        // Null = no auto-placement. Values: "WaitingToSchedule", "NeedToReturn"
        public string? ColumnRole { get; set; }

        // When set, this column is tied to a specific ST hold reason.
        // Cards with this hold reason are automatically placed here.
        public long? ServiceTitanHoldReasonId { get; set; }

        public List<BoardCard> Cards { get; set; } = new();
    }
}