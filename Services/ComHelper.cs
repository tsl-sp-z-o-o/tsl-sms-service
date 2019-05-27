using System;
using System.Threading.Tasks;
using System.IO.Ports;
using System.Threading;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TslWebApp.Services;

namespace TslWebApp.Utils
{
    internal class ComHelper : INotifyPropertyChanged, IComService

    {
        private static SerialPort sp;
        internal static string PortName { get; set; }
        internal static string[] AccessiblePorts { get; set; }

        private string answer;
        private string LastCommand = "";

        public event PropertyChangedEventHandler PropertyChanged;
  
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Answer
        {
            get
            {
                return this.answer;
            }

            set
            {
                this.answer = value;
                NotifyPropertyChanged("Answer");
            }
        }

        internal async Task Init()
        {
            if (sp == null && string.IsNullOrEmpty(PortName))
            {
                await Task.Factory.StartNew(() =>
                {
                    var PortNames = SerialPort.GetPortNames();
                    AccessiblePorts = PortNames;
                    var input = "";
                    foreach (var portName in PortNames)
                    {
                        
                        try
                        {
                            SetUpComPort(portName);
                            if (sp.IsOpen)
                            {
                                sp.WriteLine(SmsConstants.TestCmd);
                                Thread.Sleep(400);
                                input = sp.ReadExisting();

                                if (input.Length > 0)
                                {
                                    PortName = portName;
                                    sp.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
                                    sp.Close();
                                    sp.Dispose();
                                    break;
                                }
                                else
                                {
                                    sp.Close();
                                    sp.Dispose();
                                    continue;
                                }
                            }
                        }
                        catch
                        {
                            continue;
                        }

                    }
                });
            }
            else
            {
                SetUpComPort(PortName);
            }
        }

        private void SetUpComPort(string portName)
        {
            if (sp != null 
                && sp.IsOpen 
                && sp.PortName.Equals(portName))
            {
                Debug.WriteLine("Aborting COM port set up, port already set up.");
                return;
            }
            sp = new SerialPort(portName);
            sp.BaudRate = 115200;
            sp.Parity = Parity.None;
            sp.DataBits = 8;
            sp.StopBits = StopBits.One;
            sp.Handshake = Handshake.XOnXOff;
            sp.DtrEnable = true;
            sp.RtsEnable = true;
            sp.WriteTimeout = 250;
            sp.NewLine = "\r\n";

            sp.Open();
        }

        internal async Task<string> ExecuteAtCommandAsync(string command)
        {
            string message = "Error";
            await Task.Factory.StartNew(() =>
            {
                if (sp != null)
                {
                    try
                    {
                        sp.WriteLine(command);
                        LastCommand = command;
                        Debug.WriteLine("Command: "+command);
                        message = "Data written to the port.";
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(sp.PortName+" : "+ex.Message);
                    }
                }
                else
                {
                    Debug.WriteLine("No COM port set.");
                    message = "Error: No COM port set.";
                }
            });
            return message;
        }

        internal void Dispose()
        {
            sp = null;
            PortName = "";
        }

        internal void ReInit()
        {
            //TODO: Some shit here.
        }

        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort port = (SerialPort)sender;
            if (port.IsOpen && port.BytesToRead > 0 && port.BytesToWrite == 0)
            {
                var line = port.ReadLine();
                if (!line.Equals(LastCommand)
                 && !string.IsNullOrEmpty(line))
                {
                    Answer = line;
                }
                //Debug.WriteLineIf(!string.IsNullOrEmpty(line),"In: "+line);
            }
        }
    }
}
