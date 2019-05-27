using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TslWebApp.Models;
using TslWebApp.Utils.Csv;

namespace TslWebApp.Services
{
    public interface ISmsService
    {
        Task Init();
        Task<List<SmsMessage>> SendAllAsync();
        void Send(int limit, int order);
        void SendTo(List<string> numbers);
        Task CancelAsync();
        void ReInitSmsd(string portName);

        Task<bool> EditMessageAsync(EditMessageViewModel edit);
        Task<List<SmsMessage>> GetMessagesAsync(int? mid, int? driverId, int limit = 1);

        Task<List<SmsMessage>> ParseCsvFileAsync(string title, IFormFile file, bool headersExistanceMarker);

        Task AddMessagesAsync(List<SmsMessage> messages);

        Task<Tuple<string,string>> GetStatus();

        Task DeleteSmsMessage(int id);

        Task Dispose();
    }
}
