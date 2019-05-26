using System;

namespace TslWebApp.Utils.Formatters
{
    internal sealed class AtCommandFormatter
    {
        internal static string FormatSetEncodingCommand(string encoding)
        {
            return string.Format(SmsConstants.UseEncodingGsm, encoding);
        }

        internal static string FormatSetModeCommand(int mode = 1)
        {
            if (mode == 1 || mode == 0)
            {
                return string.Format(SmsConstants.ModeInst, mode);
            }
            else
            {
                throw new ArgumentException("Mode parameter cannot be value different than 0 or 1.");
            }
        }

        internal static string FormatSubmitPhoneNumber(int actualLength)
        {
            if (actualLength > 0)
            {
                return string.Format(SmsConstants.PduCmd, actualLength);
            }
            else
            {
                throw new ArgumentException("Passed phone number is invalid.");
            }
        }

        internal static string FormatSubmitPdu(string pdu)
        {
            return string.Format(SmsConstants.PduCmd, pdu);
        }

        internal static string FormatMessageSubmit(string message)
        {
            return string.Format(SmsConstants.MsgLine, message);
        }
    }
}
