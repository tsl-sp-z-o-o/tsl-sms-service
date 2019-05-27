using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TslWebApp.Models
{
    public class ListViewModel
    {
        public IEnumerable<TslWebApp.Models.SmsMessage> Messages { get; set; }
        public List<int> DeleteMessagesIds { get; set; }
    }
}
