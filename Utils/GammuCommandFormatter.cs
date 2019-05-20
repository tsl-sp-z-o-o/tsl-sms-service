namespace TslWebApp.Utils
{
    internal sealed class GammuCommandFormatter
    {
        internal static string FormatRunSmsdCommand( string confPath)
        {
            return string.Format("-c \"{0}\"", confPath);
        }

        internal static string FormatSendSmsCommand(string confPath, string number, string msg, int len)
        {
            return string.Format("-c \"{0}\" TEXT {1} -len {3} -unicode -text \"{2}\"", confPath, number, msg, len);
        }

        internal static string FormatStopSmsServiceCommand(string confPath)
        {
            return string.Format("-c \"{0}\" -k", confPath);
        }
    }
}
