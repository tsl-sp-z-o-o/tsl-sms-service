using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;
using static TslWebApp.Services.Module;

namespace TslWebApp.Services
{
    internal class ModuleService : IModuleService
    {
        private readonly IConfiguration _configuration;

        private readonly ModuleExecutor _moduleExecutor;
        private Stack<Module> registeredModuleStack = new Stack<Module>();

        public ModuleService(IConfiguration configuration)
        {
            _configuration = configuration;
            _moduleExecutor = new ModuleExecutor(_configuration, registeredModuleStack);
        }

        public bool HasOutputChanged { get; private set; }


        public void Dispose()
        {
            if (registeredModuleStack != null) {
                registeredModuleStack.Clear();
                _moduleExecutor.PurgeProcesses();
            }else
            {
                throw new InvalidOperationException("Cannot dispose uninitialized service.");
            }
        }

        public async Task ExecuteModule(string moduleName, string args = null)
        {
            await Task.Factory.StartNew(() =>
            {
                if (string.IsNullOrEmpty(moduleName))
                {
                    throw new ArgumentNullException("Module name is null or empty.");
                }
                if (registeredModuleStack.Any(module => module.ModuleName.Equals(moduleName)))
                { 
                    try
                    {
                        var moduleObject = registeredModuleStack.First(module => module.ModuleName.Equals(moduleName));
                        moduleObject.ArgumentsString = args;
                        _moduleExecutor.Execute(moduleObject);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.Message);
                        throw e;
                    }
                }
                else
                {
                    throw new ArgumentException($"Module of name {moduleName} has not been registered yet.");
                }
            });
        }

        public void Init(Stack<Module> moduleStack)
        {
            if (moduleStack != null)
            {
                if (moduleStack.Count > 0)
                {
                    registeredModuleStack = moduleStack;
                    _moduleExecutor.PropertyChanged += new PropertyChangedEventHandler(StatusChangedHandler);
                }
                else
                {
                    throw new InvalidOperationException("Passed stack is of count 0.");
                }
            }
            else
            {
                throw new ArgumentNullException("Stack is null.");
            }
        }

        internal void PurgeProcesses(string moduleName = null)
        {
            _moduleExecutor.PurgeProcesses(moduleName);
        }

        internal async Task<bool> IsAnyProcessAlive()
        {
            return await _moduleExecutor.IsAnyProcessAlive();
        }

        internal string GetStatus()
        {
            return _moduleExecutor.Status;
        }

        #region HelperMethods
        private void StatusChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            HasOutputChanged = sender != null;
        }
        #endregion

        private class ModuleExecutor : INotifyPropertyChanged
        {
            private readonly IConfiguration _configuration;

            private readonly List<Process> _processes = new List<Process>();
            private string status = "";
            private readonly Stack<Module> _registeredModules;

            public event PropertyChangedEventHandler PropertyChanged;

            internal string Status
            {
                get
                {
                    return status;
                }
                set
                {
                    this.status = value;
                    NotifyStatusChanged("Status");
                }
            }

            internal ModuleExecutor(IConfiguration configuration, Stack<Module> registeredModules)
            {
                _configuration = configuration;
                _registeredModules = registeredModules;
            }

            internal void Execute(Module module)
            {
                try
                {
                    Task.Factory.StartNew(() =>
                    {
                        var prc = new Process();

                        prc.StartInfo.FileName = DetermineExecutable(module);
                        prc.StartInfo.Arguments = string.IsNullOrEmpty(module.ArgumentsString) ? DetermineArgumentsString(module) : module.ArgumentsString;
                        prc.StartInfo.WorkingDirectory = "E:\\gammu\\";//replace from config
                        prc.StartInfo.RedirectStandardOutput = true;
                        prc.StartInfo.RedirectStandardError = true;
                        prc.StartInfo.UseShellExecute = false;
                        prc.ErrorDataReceived += new DataReceivedEventHandler(ErrOutDataReceivedHandler);
                        prc.OutputDataReceived += new DataReceivedEventHandler(StdOutDataReceivedHandler);
                        prc.Exited += new EventHandler(ExitedHandler);
                        prc.StartInfo.CreateNoWindow = true;

                        if (module.AsAdmin)
                        {
                            prc.StartInfo.UserName = new NTAccount(_configuration.GetSection("UserSettings")["AdminName"]).Value;
                            string password = _configuration.GetSection("UserSettings")["AdminPass"];
                            System.Security.SecureString ssPwd = new System.Security.SecureString();
                            for (int x = 0; x < password.Length; x++)
                            {
                                ssPwd.AppendChar(password[x]);
                            }

                            password = "";
                            prc.StartInfo.Password = ssPwd;
                        }

                        prc.Start();
                        //prc.WaitForExit();
                        prc.BeginOutputReadLine();
                        prc.BeginErrorReadLine();

                        _processes.Add(prc);
                    });
                    
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    throw e;
                }
            }

            private string DetermineArgumentsString(Module module)
            {
                switch (module.Type)
                {
                    case ModuleType.PythonModule:
                        return string.Format("\"{1}\" -c=\"{0}\" s", _configuration.GetSection("GammuSettings")["ConfPath"], module.ModulePhysicalPath);
                    default:
                        throw new InvalidOperationException("Cannot determine arguments string - unknown module type.");
                }
            }

            internal void PurgeProcesses(string moduleName = null)
            {
                _processes.RemoveAll(proc => proc.HasExited);

                if (string.IsNullOrEmpty(moduleName))
                {
                    _processes.ForEach(proc =>
                    {
                        if (!proc.HasExited)
                        {
                            proc.Kill();
                        }
                        else
                        {
                            proc.Dispose();
                        }
                    });
                }
                else
                {
                    _processes.Where(proc => 
                                     proc.StartInfo.FileName.Equals(_registeredModules.First(m => 
                                     m.ModuleName.Equals(moduleName)).ModulePhysicalPath));
                }

                _processes.Clear();
            }

            internal async Task<bool> IsAnyProcessAlive()
            {
                return await Task<bool>.Factory.StartNew(() => _processes.Find(predicate => !predicate.HasExited) != null);
            }

            private string DetermineExecutable(Module module)
            {
                switch (module.Type)
                {
                    case ModuleType.ShellModule:
                        return _configuration.GetSection("Modules")["ShellExecutablePath"];
                    case ModuleType.PythonModule:
                        return _configuration.GetSection("PythonSettings")["PythonInterpreterExec"];
                    case ModuleType.SystemModule:
                        return module.ModulePhysicalPath;
                    default:
                        throw new ArgumentException("No such module type.");
                }
            }

            private void NotifyStatusChanged(string name)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }

            private void StdOutDataReceivedHandler(object sender, DataReceivedEventArgs args)
            {
                if (args.Data != null)
                {
                    var userName = ((Process)sender).StartInfo.UserName;
                    if (string.IsNullOrEmpty(userName))
                    {
                        userName = Environment.UserName;
                    }
                    Debug.WriteLine($"{userName} : {args.Data}");
                    var printOut = args.Data.ToString();

                    if (!string.IsNullOrEmpty(printOut) 
                     && printOut.StartsWith("["))
                    {
                        Status = printOut;
                    }
                }
            }

            private void ErrOutDataReceivedHandler(object sender, DataReceivedEventArgs e)
            {
                Debug.WriteLineIf(!string.IsNullOrEmpty(e.Data), $"{((Process)sender).StartInfo.UserName} : ERROR: " + e.Data);
            }

            private void ExitedHandler(object sender, EventArgs e)
            {
                Debug.WriteLine(sender);
            }
        }
    }
}
