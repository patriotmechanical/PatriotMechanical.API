using System.ComponentModel.DataAnnotations;

namespace PatriotMechanical.API.Domain.Entities;

public class CompanySettings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string CompanyName { get; set; } = string.Empty;

    // ServiceTitan API Credentials (encrypted at rest)
    public string? ServiceTitanTenantId { get; set; }
    public string? ServiceTitanClientId { get; set; }
    public string? ServiceTitanClientSecret { get; set; }
    public string? ServiceTitanAppKey { get; set; }

    // Sync Config
    public bool AutoSyncEnabled { get; set; } = true;
    public int SyncIntervalMinutes { get; set; } = 60;
    public DateTime? LastSyncAt { get; set; }
    public string? LastSyncStatus { get; set; }

    // Markup Defaults (configurable per company)
    public decimal CreditCardFeePercent { get; set; } = 2.5m;

    // AR Alert Thresholds
    public bool ArAlertOnBalanceAmount { get; set; } = false;
    public decimal ArAlertBalanceThreshold { get; set; } = 5000m;
    public bool ArAlertOn30Days { get; set; } = false;
    public decimal ArAlertDays30Threshold { get; set; } = 0m;
    public bool ArAlertOn60Days { get; set; } = false;
    public decimal ArAlertDays60Threshold { get; set; } = 0m;
    public bool ArAlertOn90Days { get; set; } = true;
    public decimal ArAlertDays90Threshold { get; set; } = 0m;

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsServiceTitanConfigured =>
        !string.IsNullOrWhiteSpace(ServiceTitanTenantId) &&
        !string.IsNullOrWhiteSpace(ServiceTitanClientId) &&
        !string.IsNullOrWhiteSpace(ServiceTitanClientSecret) &&
        !string.IsNullOrWhiteSpace(ServiceTitanAppKey);
}