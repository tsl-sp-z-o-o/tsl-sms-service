using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace TslWebApp.Models
{
    public class SmsImportViewModel
    {
        public string Title { get; set; }
        public IFormFile Files { get; set; }
        public bool HeadersExistenceMarker { get; set; }
    }
}
