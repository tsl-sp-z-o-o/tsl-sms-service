namespace TslWebApp.Utils.Formatters
{
    internal sealed class GammuCommandFormatter
    {
        internal static string FormatRunSmsdCommand(string confPath)
        {
            return string.Format("-c \"{0}\" -s", confPath);
        }

        internal static string FormatSendSmsCommand(string confPath, string number, string msg, int len)
        {
            return string.Format("-c \"{0}\" TEXT {1} -len {3} -unicode -text \"{2}\"", confPath, number, msg, len);
        }

        internal static string FormatStopSmsServiceCommand(string confPath)
        {
            return string.Format("-c \"{0}\" -k", confPath);
        }

        internal static string FormatSmsdMonitorCommand(string configPath)
        {
            return string.Format("-c \"{0}\" -L", configPath);
        }
    }
}
