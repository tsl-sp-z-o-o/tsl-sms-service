using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TslWebApp.Data;
using TslWebApp.Models;
using TslWebApp.Utils;
using TslWebApp.Utils.Csv;
using TslWebApp.Utils.Parser;

namespace TslWebApp.Services
{
    public class SmsService : ISmsService
    {
        private bool WasInitiated = false;

        private readonly MainDbContext _mainDbContext;
        private readonly IConfiguration _configuration;
        private readonly ComHelper _comHelper;
        private readonly IGammuService _gammuService;
        //private static bool CanWrite = false;

        public SmsService(MainDbContext mainDbContext, 
                          IConfiguration configuration,
                          IComService comService,
                          IGammuService gammuService)
        {
            _mainDbContext = mainDbContext;
            _configuration = configuration;
            _comHelper = (ComHelper)comService;
            _gammuService = gammuService;
        }

        public async Task Init()
        {
            if (!WasInitiated) {
                _comHelper.PropertyChanged += new PropertyChangedEventHandler(AnswerStateChangedHandler);
                await _comHelper.Init();
                GammuConfig.Device = ComHelper.PortName;
                await _gammuService.Init();
                PrepareCsvFormatRules();
                WasInitiated = true;
            }
        }


        public async Task Dispose()
        {
            if (WasInitiated)
            {
                _mainDbContext.Dispose();
                _comHelper.Dispose();
                await _gammuService.Dispose();
            }
        }

        public void Send(int limit, int order = 0)
        {
            
        }

        public async Task<List<SmsMessage>> SendAllAsync()
        {
            var phoneNumber = _configuration.GetSection("GsmSettings")["PhoneNumber"];
            var regionPrefix = _configuration.GetSection("GsmSettings")["RegionPrefix"];

            var statusMessage = await _comHelper.ExecuteAtCommandAsync(SmsConstants.TestCmd);

            if (!statusMessage.Contains("Error"))
            {
                return await Task<List<SmsMessage>>.Factory.StartNew(() =>
                {
                    var resultMsgList = _mainDbContext.Messages.ToList();

                    resultMsgList.ForEach(async message =>
                    {
                        await _gammuService.SendSmsAsync(message);
                    });

                    return resultMsgList;
                });
            }
            else
            {
                return new List<SmsMessage>();
            }
        }

        public void SendTo(List<string> numbers)
        {
            throw new NotImplementedException();
        }

        public async Task CancelAsync(int msgId)
        {

        }

        public async Task<List<SmsMessage>> GetMessagesAsync(int? mid, int? driverId, int limit = 100)
        {
            return await Task.Factory.StartNew(() =>
            {
                var messages = _mainDbContext.Messages.ToList();
                if (mid != null && driverId != null) {
                    messages.RemoveAll(message => message.Id == mid
                                       && message.DriverId == driverId);
                }
                return messages.GetRange(0, messages.Count >= limit ? limit : messages.Count);
            });
        }


        public async Task<bool> EditMessageAsync(EditMessageViewModel edit)
        {
            var smsMessage = await _mainDbContext.Messages.FindAsync(edit.Id);
            if (smsMessage != null)
            {
                smsMessage.Content = edit.Content;
                smsMessage.PhoneNumber = edit.PhoneNumber;
            }
            else
            {
                smsMessage = new SmsMessage();
            }

           return await Task.Factory.StartNew(() => {
               var state = _mainDbContext.Messages.Update(smsMessage).State;
               _mainDbContext.SaveChanges();
               return state == Microsoft.EntityFrameworkCore.EntityState.Modified;

           });
        }

        public async Task<List<SmsMessage>> ParseCsvFileAsync(string title, IFormFile file, bool headersExistanceMarker)
        {
            var csvParser = ParserFactory.BuildCsvParser();
            var tempDir = _configuration.GetSection("Storage")["TempDir"];
            var fileStream = file.OpenReadStream();
            var targetCsvPath = Path.Combine(tempDir, title);

            if (fileStream.CanRead && fileStream.Length > 0)
            {
                var buffer = new byte[fileStream.Length];
                await fileStream.ReadAsync(buffer);
                try
                {
                    File.WriteAllBytes(targetCsvPath, buffer);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }

                _ = new List<SmsMessage>();
                var csvDocument = await csvParser.Parse(targetCsvPath, headersExistanceMarker);
                List<SmsMessage> messages;
                if (headersExistanceMarker)
                {
                    messages = ProcessCsvWithHeaders(csvDocument);
                }
                else
                {
                    messages = ProcessCsvWithoutHeaders(csvDocument);
                }
                return messages;
            }
            else
            {
                Debug.WriteLine("Couldn't read from input file stream or it was 0 length stream!");
                return new List<SmsMessage>();
            }
        }

        public async Task AddMessagesAsync(List<SmsMessage> messages)
        {
            if (messages.Count > 0)
            {
                try
                {
                    _mainDbContext.Messages.AddRange(messages);
                    await _mainDbContext.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            }
            else
            {
                Debug.WriteLine("Noo messages available.");
            }
        }

        #region HelperMethods
        private List<SmsMessage> ProcessCsvWithoutHeaders(CsvDocument csvDocument)
        {
            var smsMessageList = new List<SmsMessage>();

            var cols = csvDocument.Cols;

            if (cols.Count == CsvFormatRules.DataColCount)
            {
                int rowPtr = 0;
                Process(ref smsMessageList, ref rowPtr, ref cols);
            }
            else
            {
                Debug.WriteLine("Columns' count doesn't match column count in table.");
            }
            return smsMessageList;
        }

        private List<SmsMessage> ProcessCsvWithHeaders(CsvDocument csvDocument)
        {
            var smsMessageList = new List<SmsMessage>();

            var cols = csvDocument.Cols;

            if (cols.Count == CsvFormatRules.DataColCount)
            {   
                int rowPtr = 1;
                Process(ref smsMessageList, ref rowPtr, ref cols);
            }
            else
            {
                Debug.WriteLine("Headers' count doesn't match column count in table.");
            }
            return smsMessageList;
        }

        private void Process(ref List<SmsMessage> smsMessageList, ref int rowPtr, ref List<CsvColumn<CsvColCell<string>>> cols)
        {
            for (; rowPtr < cols[0].Cells.Count; rowPtr++)
            {
                var smsMessage = new SmsMessage();
                for (int i = CsvFormatRules.StartOffset; i <= CsvFormatRules.DataColCount; i++)
                {
                    var currentColumn = cols[i - CsvFormatRules.StartOffset];
                    var cell = currentColumn.Cells[rowPtr];

                    var value = PrepareCellValue(cell.Value, smsMessage.GetType().GetProperties()[i]);

                    smsMessage.GetType().GetProperties()[i].SetValue(smsMessage, value);
                }

                smsMessageList.Add(smsMessage);
            }
        }

        private object PrepareCellValue(string value, PropertyInfo propertyInfo)
        {
            var type = propertyInfo.PropertyType;
            if (!type.FullName.Equals(typeof(string).FullName))
            {
                if (type.IsPrimitive)
                {
                    return type.GetMethod("Parse", new Type[] { typeof(string) }).Invoke(null, new object[] { value });
                }
                else
                {
                    //Handle the error.
                    return value;
                }
            }
            return value;
        }

        public int GetMessageCount()
        {
            return _mainDbContext.Messages.Count();
        }

        
        private void AnswerStateChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            if (sender != null)
            {
                var input = ((ComHelper)sender).Answer.Trim();
                Debug.WriteLineIf(!string.IsNullOrEmpty(((ComHelper)sender).Answer), "Echo: " + input);
                if (!string.IsNullOrEmpty(input)
                 && !input.Contains("ERROR") 
                 && !input.Contains("^BOOT")
                 && (input.Contains("OK") || input.Contains(">") || input.Contains("\r\n"))
                 )
                {
                    Debug.WriteLineIf(!string.IsNullOrEmpty(((ComHelper)sender).Answer), "Answer: " + ((ComHelper)sender).Answer);
                }
            }
        }

        public void ReInitComHelper(string portName)
        {
            ComHelper.PortName = portName;
            //_comHelper.ReInit();
        }



        private async Task AddMessagesToDatabase(List<SmsMessage> messages)
        {

        }

        private void PrepareCsvFormatRules()
        {
            var mockMessage = new SmsMessage();
            var smsEntityProperties = mockMessage.GetType().GetProperties();

            CsvFormatRules.DataColCount = smsEntityProperties
                .ToList()
                .FindAll(property => property.GetCustomAttributes(true)
                .Contains(new CsvDataColumnAttribute()))
                .Count;
        }
        #endregion
    }
    }
