using System.ComponentModel.DataAnnotations;
using TslWebApp.Utils.Csv;

namespace TslWebApp.Models
{
    public class SmsMessage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [CsvDataColumn]
        [Display(Name = "Receiver", Description = "The person identity.")]
        public string Title { get; set; }

        [Required]
        [StringLength(maximumLength:1024)]
        [CsvDataColumn]
        [Display(Name = "SMS Text", Description = "The SMS content text.")]
        public string Content { get; set; }

        [Required]
        [Phone]
        [CsvDataColumn]
        [Display(Name = "Phone number", Description = "The target phone number.")]
        public string PhoneNumber { get; set; }

        [Required]
        [CsvDataColumn]
        [Display(Name = "Id", Description = "The identifier used in whole system to identify driver.")]
        public int DriverId { get; set; }
    }
}
