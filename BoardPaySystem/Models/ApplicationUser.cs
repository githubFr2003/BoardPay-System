using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System;

namespace BoardPaySystem.Models
{
    public class ApplicationUser : IdentityUser
    {
        public ApplicationUser()
        {
            FirstName = string.Empty;
            LastName = string.Empty;
        }

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(50)]
        public string LastName { get; set; }

        // Optional fields for tenants
        public int? BuildingId { get; set; }
        public int? RoomId { get; set; }
        
        // Changed from Required to NotMapped to make it compatible with the existing database
        [NotMapped]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        // This property is maintained for backward compatibility
        // It's calculated from StartDate and used by billing service
        [Range(1, 31)]
        [NotMapped]
        public int BillingCycleDay
        {
            get => StartDate.Day;
            set { /* Setter provided for backward compatibility */ }
        }

        // Navigation properties
        public virtual Building? Building { get; set; }
        public virtual Room? Room { get; set; }
    }
}
