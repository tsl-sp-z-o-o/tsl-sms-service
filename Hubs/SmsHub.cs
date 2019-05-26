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
        }

        public async Task GetMessagesStatusUpdate()
        {
            var document = await _smsService.GetMessagesStatus();
            var retVal = "Couldn't read status.";
            if (document.Length > 0) {
                retVal = document;
            }

            await Clients.User(this.Context.UserIdentifier).SendAsync("ReceiveMessagesStatus", retVal);
        }
    }
}
