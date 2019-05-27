using System.ComponentModel.DataAnnotations;

namespace TslWebApp.Models
{
    public class UserModel
    {
        [Key]
        public string Id { get; set; }
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Password")]
        public string Password { get; set; }

        [Phone]
        [Display(Name = "Phone")]
        public string PhoneNumber { get; set; }
        
    }
}
