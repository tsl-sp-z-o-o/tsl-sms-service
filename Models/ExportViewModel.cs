using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TslWebApp.Models
{
    public class ExportViewModel
    {
        public List<SmsMessage> Messages;
        public List<int> Ids { get; set; }
    }
}
