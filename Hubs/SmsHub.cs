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

        public async Task Cancel()
        {
            await _smsService.CancelAsync();
            await Startup.WipeUtilProcesses();
        }

        public async Task GetMessagesStatusUpdate()
        {
            var statusTuple = await _smsService.GetStatus();
            var retVal = "Sent count is unknown.";
            if (!string.IsNullOrEmpty(statusTuple.Item1)) {
                retVal = statusTuple.Item1;
            }

            var signalStrength = "0";

            if (!string.IsNullOrEmpty(statusTuple.Item2))
            {
                signalStrength = statusTuple.Item2;
            }

            await Clients.User(this.Context.UserIdentifier).SendAsync("ReceiveMessagesStatus", retVal, signalStrength, 21);
        }
    }
}
