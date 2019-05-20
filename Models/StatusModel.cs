using System.ComponentModel.DataAnnotations;

namespace TslWebApp.Models
{
    public class StatusModel
    {
        [Required]
        [Display(Name = "Port COM", Description = "Port COM used by application to communicate with GSM Modem.")]
        public string PortName { get; set; }

        [Required]
        [Display(Name = "Accessible COM Ports", Description = "Accessible COM ports on the system.")]
        public string[] AccessiblePorts { get; set; }

        [Display(Description = "Message count")]
        public int MessageCount { get; set; }
    }
}
