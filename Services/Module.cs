using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TslWebApp.Services
{
    public class Module
    {
        private string args;

        public string ModuleName { get; set; }
        public ModuleType Type { get; set; }
        public string ModulePhysicalPath { get; set; }

        public bool AsAdmin { get; set; } = false;

        public string ArgumentsString
        {
            get
            {
                return args;
            }
            set
            {
                args = value;
                FormatArgs();
            }
        }

        private void FormatArgs()
        {
            
        }

        public enum ModuleType
        {
            ShellModule = 0x1,
            PythonModule = 0x2,
            SystemModule = 0x3
        }
    }
}
