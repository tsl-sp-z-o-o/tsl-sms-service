using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using TslWebApp.Models;
using TslWebApp.Utils;

namespace TslWebApp.Services
{
    public class GammuService : IGammuService
    {
        private readonly IConfiguration _configuration;
        private readonly GammuExecutor _gammuExecutor;
        private readonly IGammuConfigService _gammuConfigService;

        public GammuService(IConfiguration configuration,
                            IGammuConfigService gammuConfigService)
        {
            _configuration = configuration;
            _gammuExecutor = new GammuExecutor(_configuration);
            _gammuConfigService = gammuConfigService;
        }

        public async Task Init()
        {
            var execPath = _configuration.GetSection("GammuSettings")["ExecPath"];
            var confPath = _configuration.GetSection("GammuSettings")["ConfPath"];

            await _gammuConfigService.Init(confPath);
            await _gammuConfigService.PutValue("gammu", "device", GammuConfig.Device);
            await _gammuConfigService.PutValue("smsd", "device", GammuConfig.Device);
            await _gammuConfigService.Save();

            var stopSmsServiceCmd = GammuCommandFormatter.FormatStopSmsServiceCommand(confPath);
            _gammuExecutor.ExecuteCommand(execPath, stopSmsServiceCmd, true);

            var strCmdText = $"{GammuCommandFormatter.FormatRunSmsdCommand(confPath)}";
            _gammuExecutor.ExecuteCommand(execPath, strCmdText, true);
        }

        public async Task SendSmsAsync(SmsMessage smsMessage)
        {
            await Task.Factory.StartNew(() =>
            {
                var execPath = _configuration.GetSection("GammuSettings")["InjectorExecPath"];
                var confPath = _configuration.GetSection("GammuSettings")["ConfPath"];
                var strCmdText = $"{GammuCommandFormatter.FormatSendSmsCommand(confPath, smsMessage.PhoneNumber, smsMessage.Content, smsMessage.Content.Length)}";
                _gammuExecutor.ExecuteCommand(execPath, strCmdText, true);
            });
        }

        public async Task Dispose()
        {
            _gammuConfigService.Dispose();
            await PurgeGammuProcesses();
        }

        public async Task PurgeGammuProcesses()
        {
           await Task.Factory.StartNew(() => _gammuExecutor.PurgeProcesses());
        }

        private class GammuExecutor
        {
            private readonly IConfiguration _configuration;

            private readonly List<Process> _gammuProcesses = new List<Process>();

            internal GammuExecutor(IConfiguration configuration)
            {
                _configuration = configuration;
            }

            internal void ExecuteCommand(string execPath, string strCmdText, bool asAdmin = false)
            {
                var prc = new Process();
                prc.StartInfo.FileName = execPath;
                prc.StartInfo.Arguments = strCmdText;
                prc.StartInfo.WorkingDirectory = "E:\\gammu\\";
                prc.StartInfo.RedirectStandardOutput = true;
                prc.StartInfo.RedirectStandardError = true;
                prc.StartInfo.UseShellExecute = false;
                prc.ErrorDataReceived += new DataReceivedEventHandler(ErrOutDataReceivedHandler);
                prc.OutputDataReceived += new DataReceivedEventHandler(StdOutDataReceivedHandler);
                prc.Exited += new EventHandler(ExitedHandler);
                prc.StartInfo.CreateNoWindow = true;

                if (asAdmin)
                {
                    prc.StartInfo.UserName = "Administrator";
                    string password = "@lt41r!bnL4hAd";
                    System.Security.SecureString ssPwd = new System.Security.SecureString();
                    for (int x = 0; x < password.Length; x++)
                    {
                        ssPwd.AppendChar(password[x]);
                    }
    
                    password = "";
                    prc.StartInfo.Password = ssPwd;
                }

                prc.Start();

                prc.BeginOutputReadLine();
                prc.BeginErrorReadLine();
                _gammuProcesses.Add(prc);
            }

            internal void PurgeProcesses()
            {
                _gammuProcesses.ForEach(proc => 
                {
                    proc.Kill();
                });
                _gammuProcesses.Clear();
            }

            private void StdOutDataReceivedHandler(object sender, DataReceivedEventArgs args)
            {
                Debug.WriteLine($"{((Process)sender).StartInfo.UserName} : {args.Data}");
            }
            private void ErrOutDataReceivedHandler(object sender, DataReceivedEventArgs e)
            {
                Debug.WriteLineIf(!string.IsNullOrEmpty(e.Data), $"{((Process)sender).StartInfo.UserName} : ERROR: " + e.Data);
            }

            private void ExitedHandler(object sender, EventArgs e)
            {
                Debug.WriteLine(sender);
                _gammuProcesses.Remove((Process)sender);
            }
        }
    }
}
