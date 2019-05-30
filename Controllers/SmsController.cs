using Microsoft.AspNetCore.Mvc;
using System.Linq;
using TslWebApp.Utils;
using System.Threading.Tasks;
using TslWebApp.Services;
using TslWebApp.Models;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;
using Microsoft.Extensions.Logging;
using TslWebApp.Utils.Log;
using System.IO;

namespace TslWebApp.Controllers
{
    [Authorize(Roles = "Admin, Manager")]
    public class SmsController : Controller
    {
        private readonly SmsService _smsService;

        private readonly IGammuConfigService _gammuConfigService;
        private readonly ILogger<SmsController> _logger;

        public SmsController(ISmsService smsService, 
                             IGammuConfigService gammuConfigService,
                             ILogger<SmsController> logger)
        {
            _smsService = (SmsService)smsService;
            _gammuConfigService = gammuConfigService;
            _logger = logger;
        }

        [HttpGet]
        
        public IActionResult Index()
        {
            if (!string.IsNullOrEmpty(ComHelper.PortName)) {
                return View(new SmsIndexViewModel() { });
            }

            var message = "GSM modem couldn't be found on startup, please try setting it up manually.";
            _logger.LogWarning(EventIds.HardwareEvent, message);

            if (ComHelper.AccessiblePorts.Length == 0)
            {
                message = "There is no GSM modem accessible or the driver has failed to set up COM port.";
                _logger.LogError(EventIds.HardwareEvent, message);
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
                _logger.LogError(EventIds.DatabaseEvent, message);
                TempData["ReturnMessage"] = message;
                TempData["AlertType"] = "danger";
            }
            TempData["ReturnMessage"] = message;
            TempData["AlertType"] = "success";
            return RedirectToAction(nameof(Messages));
        }

        [HttpGet]
        
        public IActionResult Import()
        {
            return View();
        }

        [HttpPost]
        
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
                _logger.LogError(EventIds.InvalidOperation, e.Message);
                TempData["ReturnMessage"] = $"The following error occured: {e.Message}";
                TempData["AlertType"] = "danger";
                return RedirectToAction(nameof(CsvValidate));
            }
            if (messageList.Count == 0) {
                _logger.LogError(EventIds.DatabaseEvent, "It seems there are no messages on the database, but action ImportConfirmation parses it, not retrieves" +
                    "from database, so the thing is more severe than it looks probably.");

                TempData["ReturnMessage"] = "It seems there are no messages currently in the database.";
                TempData["AlertType"] = "danger";
                return RedirectToAction(nameof(CsvValidate));
            }

            var serializedMsgList = JsonConvert.SerializeObject(messageList);
            var guid = Guid.NewGuid().ToString();
            this.HttpContext.Session.Set(guid, System.Text.Encoding.UTF8.GetBytes(serializedMsgList));
            return RedirectToAction(nameof(CsvValidate), new { id = guid});
        }

        [HttpGet]
        
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
        
        public async Task<IActionResult> Export()
        {
            var messages = await _smsService.GetMessagesAsync(null, null);
            return View(new ExportViewModel() { Messages = messages });
        }

        [HttpPost]
        
        [AutoValidateAntiforgeryToken]
        public async Task<FileStreamResult> ExportConfirmation(ExportViewModel exportViewModel)
        {
            var messages = await _smsService.GetMessagesAsync(null, null);
            messages = messages.FindAll(p => exportViewModel.Ids.Contains(p.Id));

            var path = await _smsService.DumpToFile(messages);
            var fStream = new FileStream(path, FileMode.Open);
            return File(fStream, "text/csv", Path.GetFileName(path));
        }

        [HttpGet]
        
        public async Task<IActionResult> EditMessage(int id)
        {
            var message = (await _smsService.GetMessagesAsync(id, null, 1))[0];
            var editMessageViewModel = new EditMessageViewModel()
            {
                Id = message.Id,
                Content = message.Content,
                PhoneNumber = message.PhoneNumber
            };
            return View(editMessageViewModel);
        }

        [HttpPost]
        
        [AutoValidateAntiforgeryToken]
        public async Task<IActionResult> EditMessageConfirmation(EditMessageViewModel editMessageViewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(nameof(EditMessage));
            }
            var isModified = await _smsService.EditMessageAsync(editMessageViewModel);
            if (!isModified) _logger.LogError(EventIds.DatabaseEvent, "Couldn't edit database.");
            TempData["ReturnMessage"] = isModified ? "The SMS message has been updated successfully." : $"The SMS message of id {editMessageViewModel.Id} couldn't be updated.";
            TempData["AlertType"] = isModified ? "success" : "danger";
            return RedirectToAction(nameof(Messages));
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        
        public async Task<IActionResult> Send(int limit = 0)
        {  
            var retMsg = "SMS service started sending SMS messages.";
            var messages = new List<SmsMessage>();
            var result = true;
            if (limit == 0)
            {
                try
                {
                    messages = await _smsService.SendAllAsync();
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogError(EventIds.InvalidOperation, ex.Message);
                    retMsg = ex.Message;
                    result = false;
                }
            }
            else
            {
                _smsService.Send(limit);
            }
            TempData["ReturnMessage"] = retMsg;
            TempData["AlertType"] = result ? "success" : "danger";
            var serializedMsgList = JsonConvert.SerializeObject(messages);
            var guid = Guid.NewGuid().ToString();
            this.HttpContext.Session.Set(guid, System.Text.Encoding.UTF8.GetBytes(serializedMsgList));
            return RedirectToAction(nameof(Progress), new {id = guid});
        }

        [HttpGet]
        
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
            _logger.LogWarning(EventIds.CommonEvent, "Updating gammu configuration.");
            await _gammuConfigService.PutValue("gammu", "device", ComHelper.PortName);
            await _gammuConfigService.PutValue("smsd", "device", ComHelper.PortName);
            await _gammuConfigService.Save();
            _logger.LogInformation(EventIds.HardwareEvent, $"COM port changed to: {modemStatusModel.PortName}");
            TempData["ReturnMessage"] = "The port was set to: "+modemStatusModel.PortName;
            TempData["AlertType"] = "danger";
            return RedirectToAction(nameof(Manage));
        }

        [HttpGet]
        
        public async Task<IActionResult> Cancel()
        {
            _logger.LogWarning(EventIds.CommonEvent, "Attempting to cancel util processes.");
            TempData["ReturnMessage"] = "Sent cancel signal to gammu service.";
            TempData["AlertType"] = "warning";
            return RedirectToAction(nameof(Index));
        }
    }
}