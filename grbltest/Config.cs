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
    }
}
