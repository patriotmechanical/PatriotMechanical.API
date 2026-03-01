using System;
using System.ComponentModel.DataAnnotations;

namespace PatriotMechanical.API.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string Email { get; set; } = null!;

        [Required]
        public string PasswordHash { get; set; } = null!;

        public string FullName { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        // Link to company
        public Guid CompanySettingsId { get; set; }
        public CompanySettings Company { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
    }
}