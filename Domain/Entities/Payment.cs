using System;

namespace PatriotMechanical.API.Domain.Entities
{
    public class Payment
    {
        public Guid Id { get; set; }

        public Guid InvoiceId { get; set; }
        public Invoice Invoice { get; set; } = null!;

        public decimal Amount { get; set; }

        public string Method { get; set; } = "Check"; // Cash, Check, CreditCard, ACH

        public decimal CreditCardFeeAmount { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    }
}