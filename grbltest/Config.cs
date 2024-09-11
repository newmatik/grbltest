using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace grbltest
{
    public class Config
    {
        public string ComPort { get; set; }
        public int JogSpeed { get; set; }
        public int RapidSpeed { get; set; }
        public int WorkAreaX { get; set; }
        public int WorkAreaY { get; set; }

        public static Config? LoadConfig(string file)
        {
            string json = File.ReadAllText(file);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<Config>(json);
        }
        public static Config? SetConfig(string port)
        {
            var configTemp = Config.LoadConfig("config.json");
            if (configTemp == null)
            {
                Log.Logging("Couldn't read config-file", Log.LogLevel.Error);
                return null;
            }

            if (!string.IsNullOrWhiteSpace(port))
            {
                string p = port.StartsWith("COM") || port.StartsWith("com") ? port.ToUpper() : $"COM{port}";

                if (configTemp.ComPort.CompareTo(p) != 0)
                {
                    Log.Logging($"Changing COM-port from {configTemp.ComPort} to {p}", Log.LogLevel.Info);
                    configTemp.ComPort = p;
                }
            }

            return configTemp;
        }
    }
}
