using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using TslWebApp.Models;

namespace TslWebApp.Services
{
    public class GammuService : IGammuService
    {
        private readonly IConfiguration _configuration;
        private readonly ModuleService _moduleService;
        private readonly IGammuConfigService _gammuConfigService;

        private static string configurationPath;

        public GammuService(IConfiguration configuration,
                            IGammuConfigService gammuConfigService,
                            IModuleService moduleService)
        {
            _configuration = configuration;
            _moduleService = (ModuleService)moduleService;
            
            _gammuConfigService = gammuConfigService;
        }

        public async Task Init()
        {
            configurationPath = _configuration.GetSection("GammuSettings")["ConfPath"];

            await _gammuConfigService.Init(configurationPath);
            await _gammuConfigService.PutValue("gammu", "device", GammuConfig.Device);
            await _gammuConfigService.PutValue("smsd", "device", GammuConfig.Device);
            await _gammuConfigService.Save();

            await _moduleService.ExecuteModule("GammuSmsd", new string[] { configurationPath, "-k" });
            await _moduleService.ExecuteModule("run-smsd", new string[] { configurationPath });
        }

        public async Task SendSmsAsync(SmsMessage smsMessage)
        {
                await _moduleService.ExecuteModule("GammuSmsdInjector", new string[] {
                                                    configurationPath,
                                                    smsMessage.PhoneNumber,
                                                    smsMessage.Content,
                                                    smsMessage.Content.Length.ToString()
                });
        }

        public async Task Dispose()
        {
            _gammuConfigService.Dispose();
            await PurgeGammuProcesses();
        }

        public async Task PurgeGammuProcesses()
        {
           await Task.Factory.StartNew(() => _moduleService.PurgeProcesses());
        }

        public async Task<string> CheckSmsStatus()
        {
            await _moduleService.ExecuteModule("smsd-checker", new string[] { configurationPath, "" });
            return _moduleService.HasOutputChanged ? _moduleService.GetStatus() : "";
        }

        public async Task<bool> IsGammuAliveAsync()
        {
            return await _moduleService.IsAnyProcessAlive();
        }
    }
}
