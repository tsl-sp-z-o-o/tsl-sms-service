using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TslWebApp.Models;
using TslWebApp.Utils.Log;

namespace TslWebApp.Controllers
{
    public class HomeController : Controller
    {
        private IApplicationLifetime ApplicationLifetime { get; set; }
        private ILogger Logger { get; set; }

        public HomeController(IApplicationLifetime applicationLifetime,
                              ILogger<HomeController> logger)
        {
            ApplicationLifetime = applicationLifetime;
            Logger = logger;
        }

        [AllowAnonymous]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [AllowAnonymous]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public IActionResult Shutdown()
        {
            
            Task.Factory.StartNew(() => 
            {
                Thread.Sleep(1000);
                Logger.LogWarning(EventIds.CommonEvent, "Shutding down system.");
                HttpContext.Session.Clear();
                ApplicationLifetime.StopApplication();
            });
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult About()
        {
            ViewData["Version"] = Assembly.GetEntryAssembly().GetName().Version.ToString();
            ViewData["OsName"] = Environment.OSVersion;
            return View();
        }
    }
}
