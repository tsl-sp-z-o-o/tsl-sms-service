using System;
using System.Collections.Generic;

namespace TslWebApp.Models
{
    public class CsvValidateViewModel
    {
        public string Id { get; set; }
        public List<SmsMessage> Messages { get; set; }
    }
}
