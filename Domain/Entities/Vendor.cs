using System.ComponentModel.DataAnnotations;

namespace PatriotMechanical.API.Domain.Entities;

public class Vendor
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public ICollection<ApBill> Bills { get; set; } = new List<ApBill>();
}