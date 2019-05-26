using System;
using System.Diagnostics;
using System.Threading.Tasks;
using IniParser;
using IniParser.Model;

namespace TslWebApp.Services
{
    public class GammuConfigService : IGammuConfigService
    {
        private FileIniDataParser dataParser;
        private static string globalConfPath;
        private static IniData dataSnapshot;

        public void Dispose()
        {
            Save().Wait();
            globalConfPath = "";
            dataParser = null;
        }

        public async Task<IniData> GetIniData()
        {
            return await Task<IniData>.Factory.StartNew(() =>
            {
                dataSnapshot = dataParser.ReadFile(globalConfPath);
                return dataSnapshot;
            });
        }

        public async Task<string> GetValue(string section, string valueName)
        {
            if (dataSnapshot == null)
            {
                await GetIniData();
            }
            return dataSnapshot[section][valueName];
        }

        public async Task Init(string confPath)
        {
            dataParser = new FileIniDataParser();
            globalConfPath = confPath;
            await GetIniData();
        }

        public async Task<bool> PutValue(string section, string valueName, object value)
        {
            try
            {
                await Task.Factory.StartNew(() =>
                {
                    var tmpIniData = new IniData();
                    tmpIniData[section][valueName] = value != null ? value.ToString() : string.Empty;
                    dataSnapshot.Merge(tmpIniData);
                });
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return false;
            }
            return true;
        }

        public async Task<bool> Save()
        {
            return await Task<bool>.Factory.StartNew(() =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(globalConfPath) && dataSnapshot != null)
                    {
                        dataParser.WriteFile(globalConfPath, dataSnapshot);
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    return false;
                }
                return true;
            });
        }
    }
}
