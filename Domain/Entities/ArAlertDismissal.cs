namespace PatriotMechanical.API.Domain.Entities;

public class ArAlertDismissal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanySettingsId { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime DismissedAt { get; set; } = DateTime.UtcNow;
    public string DismissedBy { get; set; } = string.Empty;

    // Navigation
    public Customer? Customer { get; set; }
}