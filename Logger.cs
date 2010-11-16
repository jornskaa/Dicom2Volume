using System;
using System.Globalization;
using System.IO;

namespace Dicom2Volume
{
    public class Logger
    {
        public enum LogLevelType
        {
            Error = 3, 
            Warn  = 2, 
            Info  = 1, 
            Debug = 0
        }

        public static void Warn(string message, params object[] arguments)
        {
            Log(LogLevelType.Warn, message, arguments);
        }

        public static void Error(string message, params object[] arguments)
        {
            Log(LogLevelType.Error, message, arguments);
        }

        public static void Info(string message, params object[] arguments)
        {
            Log(LogLevelType.Info, message, arguments);
        }

        public static void Debug(string message, params object[] arguments)
        {
            Log(LogLevelType.Debug, message, arguments);
        }

        public static void Log(LogLevelType levelType, string message, params object[] arguments)
        {
            var logMessage = levelType + "::" + DateTime.Now.ToString(CultureInfo.InvariantCulture) + "::" + String.Format(message, arguments);
            var previousForegroundColor = Console.ForegroundColor;
            var messageColor = Console.ForegroundColor;
            switch (levelType)
            {
                case LogLevelType.Warn:
                    messageColor = ConsoleColor.DarkMagenta;
                    break;
                case LogLevelType.Error:
                    messageColor = ConsoleColor.DarkRed;
                    break;
                case LogLevelType.Info:
                    messageColor = ConsoleColor.DarkGreen;
                    break;
                case LogLevelType.Debug:
                    messageColor = ConsoleColor.DarkYellow;
                    break;
            }

            if (levelType < Config.LogLevel) return;

            Console.ForegroundColor = messageColor;
            Console.WriteLine(logMessage);
            Console.ForegroundColor = previousForegroundColor;
        }
    }
}
