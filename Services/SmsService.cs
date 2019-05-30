using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
using TslWebApp.Utils.Log;
using TslWebApp.Utils.Parser;
using static System.Net.Mime.MediaTypeNames;

namespace TslWebApp.Services
{
    public class SmsService : ISmsService
    {
        private bool WasInitiated = false;

        private readonly MainDbContext _mainDbContext;
        private readonly IConfiguration _configuration;
        private readonly ComHelper _comHelper;
        private readonly IGammuService _gammuService;
        private readonly ILogger _logger;

        //private static bool CanWrite = false;

        public SmsService(MainDbContext mainDbContext, 
                          IConfiguration configuration,
                          IComService comService,
                          IGammuService gammuService,
                          ILogger<ISmsService> logger)
        {
            _mainDbContext = mainDbContext;
            _configuration = configuration;
            _comHelper = (ComHelper)comService;
            _gammuService = gammuService;
            _logger = logger;
        }

        public async Task Init()
        {
            _logger.LogInformation("Initializing...");
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
                _logger.LogInformation("Disposing...");
                _mainDbContext.Dispose();
                _comHelper.Dispose();
                await _gammuService.Dispose();
            }
        }

        public void Send(int limit, int order = 0)
        {
            //not used now.
        }

        public async Task<List<SmsMessage>> SendAllAsync()
        {
            _logger.LogInformation("Starting send all operation.");
            if (true)//temporarily
            {
                return await Task<List<SmsMessage>>.Factory.StartNew(() =>
                {
                    var resultMsgList = _mainDbContext.Messages.ToList();

                    resultMsgList.ForEach(async message =>
                    {
                        await _gammuService.SendSmsAsync(message);
                    });
                    _logger.LogInformation("All messages submitted.");
                    return resultMsgList;
                });
            }
            else
            {
                throw new InvalidOperationException("There was an error while running");
            }
        }

        public void SendTo(List<string> numbers)
        {
            throw new NotImplementedException();
        }

        public async Task CancelAsync()
        {
            _logger.LogInformation("Purging util processes...");
            await _gammuService.PurgeGammuProcesses();
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
               _logger.LogWarning("Database content changed, changes saved.");
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
                    _logger.LogError(EventIds.IOEvent, $"Error occurred: {e.Message}");
                }

                _ = new List<SmsMessage>();
                var csvDocument = await csvParser.Parse(targetCsvPath, headersExistanceMarker);
                List<SmsMessage> messages;
                try
                {
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
                catch (ArgumentException ex)
                {
                    _logger.LogError(EventIds.InvalidOperation, $"Error occurred: {ex.Message}");
                    throw ex;
                }
                catch (FormatException ex)
                {
                    _logger.LogError(EventIds.InvalidOperation, $"Error occurred: {ex.Message}");
                    throw ex;
                }
                
            }
            _logger.LogError($"Couldn't read from input file stream or it was 0 length stream!");
            throw new ArgumentException("Non-readable stream provided in IFileForm object.");
        }

        public async Task<string> DumpToFile(List<SmsMessage> messages)
        {
            return await Task.Factory.StartNew(() =>
            {
                CsvDocument csvDocument = new CsvDocument();
                var colCount = CsvFormatRules.DataColCount;

                var cols = new List<CsvColumn<CsvColCell<string>>>();
                var csvDataProperties = typeof(SmsMessage).GetProperties()
                                        .ToList()
                                        .FindAll(property => property.GetCustomAttributes(true)
                                        .Contains(new CsvDataColumnAttribute()));

                foreach (var dataProperty in csvDataProperties)
                {
                    var cells = new List<CsvColCell<string>>();
                    var col = new CsvColumn<CsvColCell<string>>("", cells);

                    messages.ForEach(message =>
                    {
                        var value = dataProperty.GetValue(message);
                        var cell = new CsvColCell<string>();
                        cell.Index = cells.Count;
                        cell.ParentColumn = col;
                        cell.Value = value.ToString();
                        cells.Add(cell);

                    });
                    cols.Add(col);
                }
                csvDocument.Cols = cols;
                var path = CreatePath();
                csvDocument.ToFile(path);
                return path;
            });
        }

        private string CreatePath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                              "tsl",
                                              $"export-{DateTime.Now}.csv".Replace(":", "-"))
                                             .Replace(" ", "_");
        }

        public async Task AddMessagesAsync(List<SmsMessage> messages)
        {
            if (messages.Count > 0)
            {
                try
                {
                    foreach (var message in messages)
                    {
                        var isKnown = _mainDbContext.Messages.Any(m => m.DriverId == message.DriverId);
                        if (isKnown)
                        {
                            var tmp = _mainDbContext.Messages.First(m => m.DriverId == message.DriverId);
                            _mainDbContext.Messages.Update(tmp);
                        }
                        else
                        {
                            await _mainDbContext.Messages.AddAsync(message);
                        }
                    }
                    
                    await _mainDbContext.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    _logger.LogError(EventIds.DatabaseEvent, $"Error occurred: {e.Message}");
                }
            }
            else
            {
                _logger.LogError(EventIds.InvalidOperation, "No messages available.");
            }
        }

        public async Task<Tuple<string, string>> GetStatus()
        {
            try
            {
                var dataString = await _gammuService.CheckSmsStatus();
                
                var messageCount = "";
                var signalStrength = "";
                if (!string.IsNullOrEmpty(dataString)) {
                    var dataStrings = dataString.Split("-");
                    
                    var document = await ParserFactory.BuildCsvParser().ParseLine(dataStrings[1]);
                    messageCount = document.Cols[0].Cells[0].Value;
                    signalStrength = document.Cols[0].Cells[2].Value;
                }
                return new Tuple<string,string>(messageCount, signalStrength);
            }
            catch (Exception e)
            {
                _logger.LogError(EventIds.InvalidOperation, $"Error occurred: {e.Message}");
                throw e;
            }
        }

        public async Task DeleteSmsMessage(int id)
        {
            _logger.LogInformation(EventIds.CommonEvent, $"Deleting message of id {id}");
            var messageModel = _mainDbContext.Messages.First(message => message.Id == id);

            _mainDbContext.Remove(messageModel);
            await _mainDbContext.SaveChangesAsync();
            _logger.LogWarning(EventIds.DatabaseEvent, "Changes saved to database.");
        }

        #region HelperMethods
        private List<SmsMessage> ProcessCsvWithoutHeaders(CsvDocument csvDocument)
        {
            var smsMessageList = new List<SmsMessage>();

            var cols = csvDocument.Cols;

            if (cols.Count == CsvFormatRules.DataColCount)
            {
                int rowPtr = 0;
                try
                {
                    Process(ref smsMessageList, ref rowPtr, ref cols);
                }
                catch (ArgumentException ex)
                {
                    _logger.LogError(EventIds.InvalidOperation, ex.Message);
                    throw ex;
                }
            }
            else
            {
                Debug.WriteLine("Columns' count doesn't match column count in table.");
                throw new FormatException("The csv file has the unexpected format.");
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
                try
                {
                    Process(ref smsMessageList, ref rowPtr, ref cols);
                }
                catch (ArgumentException ex)
                {
                    throw ex;
                }
            }
            else
            {
                Debug.WriteLine("Headers' count doesn't match column count in table.");
                throw new FormatException("The csv file has the unexpected format.");
            }
            return smsMessageList;
        }

        private void Process(ref List<SmsMessage> smsMessageList, 
                             ref int rowPtr, ref List<CsvColumn<CsvColCell<string>>> cols)
        {
            for (; rowPtr < cols[0].Cells.Count; rowPtr++)
            {
                var smsMessage = new SmsMessage();
                for (int i = CsvFormatRules.StartOffset; i <= CsvFormatRules.DataColCount; i++)
                {
                    var currentColumn = cols[i - CsvFormatRules.StartOffset];
                    var cell = currentColumn.Cells[rowPtr];
                    try
                    {
                        var value = PrepareCellValue(cell.Value, smsMessage.GetType().GetProperties()[i]);
                        smsMessage.GetType().GetProperties()[i].SetValue(smsMessage, value);
                    }
                    catch (ArgumentException ex)
                    {
                        _logger.LogError(EventIds.InvalidOperation, ex.Message);
                        throw ex;
                    }
                   
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
                    throw new ArgumentException("The string value should represent an actual string or antoher primitve value.");
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
            }
        }

        public void ReInitSmsd(string portName)
        {
            ComHelper.PortName = portName;
            _gammuService.Init().Wait();
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
