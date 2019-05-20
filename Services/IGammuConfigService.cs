using IniParser.Model;
using System.Threading.Tasks;

namespace TslWebApp.Services
{
    public interface IGammuConfigService
    {
        Task Init(string confPath);

        Task<IniData> GetIniData();

        Task<string> GetValue(string section, string valueName);

        Task<bool> PutValue(string section, string valueName, object value);

        Task<bool> Save();

        void Dispose();
    }
}
