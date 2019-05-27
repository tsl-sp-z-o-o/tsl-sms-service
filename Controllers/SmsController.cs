using Microsoft.AspNetCore.Mvc;
using TslWebApp.Data;
using System.Linq;
using TslWebApp.Utils;
using System.Threading.Tasks;
using TslWebApp.Services;
using TslWebApp.Models;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;
using Microsoft.AspNetCore.SignalR;
using TslWebApp.Hubs;
using Microsoft.AspNetCore.Identity;
using System.Diagnostics;

namespace TslWebApp.Controllers
{
    public class SmsController : Controller
    {
        private readonly SmsService _smsService;
        private readonly IHubContext<SmsHub> _smsHubContext;
        private readonly IGammuConfigService _gammuConfigService;
        private readonly UserManager<User> _userManager;

        public SmsController(ISmsService smsService, 
                             IHubContext<SmsHub> smsHubContext,
                             IGammuConfigService gammuConfigService,
                             UserManager<User> userManager)
        {
            _smsService = (SmsService)smsService;
            _smsHubContext = smsHubContext;
            _gammuConfigService = gammuConfigService;
            _userManager = userManager;
        }

        [HttpGet]
        [Authorize(Roles = "Admin, Manager")]
        public IActionResult Index()
        {
            if (!string.IsNullOrEmpty(ComHelper.PortName)) {
                return View(new SmsIndexViewModel() { });
            }

            var message = "GSM modem couldn't be found on startup, please try setting it up manually.";

            if (ComHelper.AccessiblePorts.Length == 0)
            {
                message = "There is no GSM modem accessible or the driver has failed to set up COM port.";
            }

            TempData["ReturnMessage"] = message;
            TempData["AlertType"] = "warning";
            return RedirectToAction(nameof(Manage));
        }

        [HttpGet]
        [Authorize(Roles ="Admin, Manager")]
        public IActionResult Manage()
        {
            var modemStatusModel = new StatusModel()
            {
                PortName = ComHelper.PortName,
                AccessiblePorts = ComHelper.AccessiblePorts,
                MessageCount = _smsService.GetMessageCount()
            };

            var tmpList = modemStatusModel.AccessiblePorts.ToList();
            tmpList.RemoveAll(e => e.Equals(modemStatusModel.PortName));
            modemStatusModel.AccessiblePorts = tmpList.ToArray<string>();

            return View(modemStatusModel);
        }

        [HttpGet]
        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> Messages(string av = "list")
        {
            var messageList = await _smsService.GetMessagesAsync(null, null);
            ViewData["ActionType"] = av;
            var listViewModel = new ListViewModel();
            listViewModel.Messages = messageList;
            return View(listViewModel);
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Authorize(Roles = "Admin, Manager")]
        public IActionResult DeleteMessages(ListViewModel listViewModel)
        {

            var ids = listViewModel.DeleteMessagesIds;
            var message = "Deleted successfully.";
            try
            {
                ids.ForEach(id =>
                {
                    _smsService.DeleteSmsMessage(id).Wait();
                });
            }
            catch (Exception e)
            {
                message = $"Couldn't delete because of the following error: {e.Message}";
                TempData["ReturnMessage"] = message;
                TempData["AlertType"] = "danger";
            }
            TempData["ReturnMessage"] = message;
            TempData["AlertType"] = "success";
            return RedirectToAction(nameof(Messages));
        }

        [HttpGet]
        [Authorize(Roles = "Admin, Manager")]
        public IActionResult Import()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Admin, Manager")]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> ImportConfirmation(SmsImportViewModel smsImportViewModel)
        {
            var messageList = new List<SmsMessage>();
            try
            {
                messageList = await _smsService.ParseCsvFileAsync(smsImportViewModel.Title, smsImportViewModel.Files, smsImportViewModel.HeadersExistenceMarker);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                TempData["ReturnMessage"] = $"The following error occured: {e.Message}";
                TempData["AlertType"] = "danger";
                return RedirectToAction(nameof(CsvValidate));
            }
            if (messageList.Count == 0) {
                TempData["ReturnMessage"] = "Unknown error occured.";
                TempData["AlertType"] = "danger";
                return RedirectToAction(nameof(CsvValidate));
            }

            var serializedMsgList = JsonConvert.SerializeObject(messageList);
            var guid = Guid.NewGuid().ToString();
            this.HttpContext.Session.Set(guid, System.Text.Encoding.UTF8.GetBytes(serializedMsgList));
            return RedirectToAction(nameof(CsvValidate), new { id = guid});
        }

        [HttpGet]
        [Authorize(Roles = "Admin, Manager")]
        public IActionResult CsvValidate(string id)
        {
            var messageList = new List<SmsMessage>();
            if (!string.IsNullOrEmpty(id))
            {
                var isListAccessible = this.HttpContext.Session.TryGetValue(id, out byte[] data);
                if (isListAccessible)
                {
                   messageList = JsonConvert.DeserializeObject<List<SmsMessage>>(System.Text.Encoding.UTF8.GetString(data));
                }
            }
            return View(new CsvValidateViewModel { Messages = messageList, Id = id });
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> CsvValidateConfirmation(bool isValid, string id)
        {
            if (isValid)
            {
                var isListAccessible = this.HttpContext.Session.TryGetValue(id, out byte[] data);
                if (isListAccessible)
                {
                    var messageList = JsonConvert.DeserializeObject<List<SmsMessage>>(System.Text.Encoding.UTF8.GetString(data));
                    await _smsService.AddMessagesAsync(messageList);
                }
                
                return RedirectToAction(nameof(Messages));
            }

            return RedirectToAction(nameof(Import));
        }

        [HttpGet]
        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> EditMessage(int id)
        {
            var message = (await _smsService.GetMessagesAsync(id, null))[0];//There is a formal parameter: limit = 100, 
                                                                            //there will be always one message inside the list!
            var editMessageViewModel = new EditMessageViewModel()
            {
                Id = message.Id,
                Content = message.Content,
                PhoneNumber = message.PhoneNumber
            };
            return View(editMessageViewModel);
        }

        [HttpPost]
        [Authorize(Roles = "Admin, Manager")]
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> EditMessageConfirmation(EditMessageViewModel editMessageViewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(nameof(EditMessage));
            }
            var isModified = await _smsService.EditMessageAsync(editMessageViewModel);
            TempData["ReturnMessage"] = isModified ? "The SMS message has been updated successfully." : $"The SMS message of id {editMessageViewModel.Id} couldn't be updated.";
            TempData["AlertType"] = isModified ? "success" : "danger";
            return RedirectToAction(nameof(Messages));
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> Send(int limit = 0)
        {  
            var retMsg = "SMS service started sending SMS messages.";
            var messages = new List<SmsMessage>();
            var result = true;
            if (limit == 0)
            {
                try
                {
                    //messages = await _smsService.SendAllAsync();

                }
                catch (InvalidOperationException ex)
                {
                    retMsg = ex.Message;
                    result = false;
                }
            }
            else
            {
                //TODO: What if limit is changed.
            }
            TempData["ReturnMessage"] = retMsg;
            TempData["AlertType"] = result ? "success" : "danger";
            var serializedMsgList = JsonConvert.SerializeObject(messages);
            var guid = Guid.NewGuid().ToString();
            this.HttpContext.Session.Set(guid, System.Text.Encoding.UTF8.GetBytes(serializedMsgList));
            return RedirectToAction(nameof(Progress), new {id = guid});
        }

        [HttpGet]
        [Authorize(Roles = "Admin, Manager")]
        public IActionResult Progress(string id)
        {
            var isListAccessible = this.HttpContext.Session.TryGetValue(id, out byte[] data);
            if (isListAccessible)
            {
                var messages = JsonConvert.DeserializeObject<List<SmsMessage>>(System.Text.Encoding.UTF8.GetString(data));
                return View(messages);
            }
            TempData["ReturnMessage"] = $"Couldn't read list of messages sent.";
            TempData["AlertType"] = "danger";
            return View();
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditConfiguration(StatusModel modemStatusModel)
        {
            if (ComHelper.AccessiblePorts.Length == 0) {
                return RedirectToAction(nameof(Manage));
            }
            _smsService.ReInitSmsd(modemStatusModel.PortName);
            await _gammuConfigService.PutValue("gammu", "device", ComHelper.PortName);
            await _gammuConfigService.PutValue("smsd", "device", ComHelper.PortName);
            await _gammuConfigService.Save();
            TempData["ReturnMessage"] = "The port was set to: "+modemStatusModel.PortName;
            TempData["AlertType"] = "danger";
            return RedirectToAction(nameof(Manage));
        }

        [HttpGet]
        [Authorize(Roles = "Admin, Manager")]
        public async Task<IActionResult> Cancel()
        {
            await _smsService.CancelAsync();
            TempData["ReturnMessage"] = "Sent cancel signal to gammu service.";
            TempData["AlertType"] = "warning";
            return RedirectToAction(nameof(Progress));
        }
    }
}