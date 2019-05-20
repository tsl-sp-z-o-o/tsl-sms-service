using System;
using System.Collections.Generic;

namespace TslWebApp.Models
{
    public class SmsIndexViewModel
    {
        public DateTime LastTimeSent { get; set; } = new DateTime();
        public List<SmsMessage> Messages { get; set; }
    }
}
