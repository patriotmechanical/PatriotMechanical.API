namespace PatriotMechanical.API.Domain.Entities
{
    public class BoardColumn
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public int SortOrder { get; set; }
        public string Color { get; set; } = "#334155"; // hex color for column header
        public bool IsDefault { get; set; } // seeded columns can't be deleted
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<BoardCard> Cards { get; set; } = new();
    }
}