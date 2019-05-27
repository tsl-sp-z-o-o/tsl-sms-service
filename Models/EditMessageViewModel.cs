using System.ComponentModel.DataAnnotations;

namespace TslWebApp.Models
{
    public class EditMessageViewModel
    {
        [Key]
        [Required]
        public int Id { get; set; }

        [StringLength(maximumLength: 1024, ErrorMessage = "The message cannot be longer than 1024 characters.")]
        [Required]
        [Display(Description = "Content of SMS message", Name = "SMS message content")]
        [Editable(true)]
        public string Content { get; set; }

        [Required]
        [Display(Description = "Target phone number", Name = "Target phone number")]
        [Editable(true)]
        [Phone]
        [StringLength(9, ErrorMessage = "Phone number must be 9 characters length.", MinimumLength = 9)]
        public string PhoneNumber { get; set; }

        [Required]
        [Display(Name = "Driver id", Description = "Driver's identifier in the system.")]
        [Editable(true)]
        public int DriverId { get; set; }

    }
}
