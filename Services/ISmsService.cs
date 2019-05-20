using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using TslWebApp.Models;

namespace TslWebApp.Services
{
    public interface ISmsService
    {
        Task Init();
        Task<List<SmsMessage>> SendAllAsync();
        void Send(int limit, int order);
        void SendTo(List<string> numbers);
        Task CancelAsync(int msgId);
        void ReInitComHelper(string portName);

        Task<bool> EditMessageAsync(EditMessageViewModel edit);
        Task<List<SmsMessage>> GetMessagesAsync(int? mid, int? driverId, int limit = 1);

        Task<List<SmsMessage>> ParseCsvFileAsync(string title, IFormFile file, bool headersExistanceMarker);

        Task AddMessagesAsync(List<SmsMessage> messages);

        Task Dispose();
    }
}
