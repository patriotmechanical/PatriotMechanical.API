namespace PatriotMechanical.API.Domain.Entities
{
    public class BoardCardNote
    {
        public Guid Id { get; set; }
        public Guid BoardCardId { get; set; }
        public BoardCard Card { get; set; } = null!;

        public string Text { get; set; } = null!;
        public string? Author { get; set; } // user who added the note
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}