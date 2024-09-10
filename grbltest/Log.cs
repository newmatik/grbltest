using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace grbltest
{
    public class Log
    {
        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error
        }
        public static void Logging(string message, LogLevel level, bool writeToFile = false)
        {
            string dt = DateTime.Now.ToString("HH-mm-ss");
            switch (level)
            {
                case LogLevel.Debug:
                    Console.ForegroundColor = ConsoleColor.Gray;
                    break;
                case LogLevel.Info:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
                case LogLevel.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogLevel.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
            }
            Console.WriteLine($"{dt} {message}");

            if(writeToFile)
            {
                // Write to file
                using (StreamWriter sw = new StreamWriter("grbl_log.txt", true))
                {
                    sw.WriteLine($"[{dt}] Command: {message}");
                }
            }
        }
    }
}
