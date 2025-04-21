using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoardPaySystem.Models
{
    
    public class Users
    {
        [Key]
        public int userID { get; set; }
        [MaxLength(50), Required] 
        public string firstName { get; set; } = " ";
        [MaxLength(50), Required]
        public string lastName { get; set; } = " ";
        [MaxLength(100), Required]
        public string username { get; set; } = " ";
        [MaxLength(15), Required]
        public string phone { get; set; }
        [MaxLength(255), Required]
        public string password { get; set; } = " ";
        [ForeignKey("roleID"), Required]
        public int roleID { get; set; }

        public Role Role { get; set; }
    }
}
