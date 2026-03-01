namespace PatriotMechanical.API.Domain.Entities
{
    public class WarrantyClaimNote
    {
        public Guid Id { get; set; }
        public Guid WarrantyClaimId { get; set; }
        public WarrantyClaim Claim { get; set; } = null!;

        public string Text { get; set; } = null!;
        public string? Author { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}