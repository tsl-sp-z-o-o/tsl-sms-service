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
        public string Title { get; set; }

        [Required]
        [StringLength(maximumLength:1024)]
        [CsvDataColumn]
        public string Content { get; set; }

        [Required]
        [Phone]
        [CsvDataColumn]
        public string PhoneNumber { get; set; }

        [Required]
        [CsvDataColumn]
        public int DriverId { get; set; }
    }
}
