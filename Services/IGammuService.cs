using System.Threading.Tasks;
using TslWebApp.Models;

namespace TslWebApp.Services
{
    public interface IGammuService
    {
        Task Init();

        Task SendSmsAsync(SmsMessage smsMessage);

        Task PurgeGammuProcesses();
        Task Dispose();
    }
}
