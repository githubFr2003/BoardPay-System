using System.ComponentModel.DataAnnotations;


namespace BoardPaySystem.Models
{
   public class Role
    {
        [Key]
        public int roleID { get; set; }
        [MaxLength(255)]
        public string Name { get; set; } = string.Empty;
    }
}
