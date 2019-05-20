using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using TslWebApp.Models;

namespace TslWebApp.Controllers
{
    public class HomeController : Controller
    {
        private IApplicationLifetime ApplicationLifetime { get; set; }

        public HomeController(IApplicationLifetime applicationLifetime)
        {
            ApplicationLifetime = applicationLifetime;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult Shutdown()
        {
            Task.Factory.StartNew(() => 
            {
                Thread.Sleep(1000);
                ApplicationLifetime.StopApplication();
            });
            return View();
        }
    }
}
