using System.IO;
using System.Threading.Tasks;
using TslWebApp.Models;

namespace TslWebApp.Services
{
    public interface IGammuService
    {
        Task Init();

        Task SendSmsAsync(SmsMessage smsMessage);

        Task<string> CheckSmsStatus();

        Task PurgeGammuProcesses();

        Task<bool> IsGammuAliveAsync();

        Task Dispose();
    }
}
