using System;

namespace TslWebApp.Utils
{
    internal static class SmsConstants
    {
        internal const string ModeInst = "AT+CMGF={0}\r\n";
        internal const string UseEncodingGsm = "AT+CSCS=\"{0}\"\r\n";
        internal const string PduCmd = "AT+CMGS={0}\r\n";
        internal static string MsgLine = "{0}\r\n";
        internal static string TestCmd = "AT\n";
        internal static string SubmitCmd = "\x1A\n";
        internal static int MaxLength = 150;
    }
}
