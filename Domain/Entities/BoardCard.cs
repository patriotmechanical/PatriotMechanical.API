namespace PatriotMechanical.API.Domain.Entities
{
    public class BoardCard
    {
        public Guid Id { get; set; }
        public Guid BoardColumnId { get; set; }
        public BoardColumn Column { get; set; } = null!;

        public Guid? WorkOrderId { get; set; }
        public WorkOrder? WorkOrder { get; set; }

        public string JobNumber { get; set; } = null!;
        public string? CustomerName { get; set; }
        public int SortOrder { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        public List<BoardCardNote> Notes { get; set; } = new();
    }
}