namespace PatriotMechanical.API.Domain.Entities
{
    public class ServiceTitanSyncState
    {
        public int Id { get; set; }

        public string EntityName { get; set; } = null!;

        public string? ContinuationToken { get; set; }

        public DateTime LastSynced { get; set; }
    }
}