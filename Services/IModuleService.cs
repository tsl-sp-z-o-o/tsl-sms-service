using System.Collections.Generic;
using System.Threading.Tasks;

namespace TslWebApp.Services
{
    public interface IModuleService
    {
        void Init(Stack<Module> modules);

        Task ExecuteModule(string moduleName, string args);
        void Dispose();
    }
}