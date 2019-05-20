using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using TslWebApp.Services;

namespace TslWebApp.Hubs
{
    public class SmsHub : Hub
    {
        private readonly ISmsService _smsService;
        public SmsHub(ISmsService smsService)
        {
            _smsService = smsService;
        }

        public async Task CancelMessage(int msgId)
        {
           await _smsService.CancelAsync(msgId);
        }
    }
}
