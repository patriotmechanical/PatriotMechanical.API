using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PatriotMechanical.API.Domain.Entities;

public class ApBill
{
    public Guid Id { get; set; }
    public decimal TotalAmount { get; set; }
    public Guid VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public decimal Amount { get; set; }
    public DateTime DueDate { get; set; }

    public bool IsPaid { get; set; }

    public DateTime CreatedOn { get; set; }
}