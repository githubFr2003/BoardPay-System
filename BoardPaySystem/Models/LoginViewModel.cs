// Models/LoginViewModel.cs
using System.ComponentModel.DataAnnotations;

namespace BoardPaySystem.Models // Use your actual project namespace
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username is required.")] // Add error messages
        public required string Username { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [DataType(DataType.Password)] // Helps render as password input
        public required string Password { get; set; }

        [Required(ErrorMessage = "Please select a role.")]
        public string Role { get; set; }

    }

}