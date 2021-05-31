using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Battery.Internal
{
    internal class GHubBatteryStats
    {
        [JsonProperty(PropertyName = "isCharging")]
        public bool IsCharging { get; set; }

        [JsonProperty(PropertyName = "millivolts")]
        public int Millivolts { get; set; }

        [JsonProperty(PropertyName = "percentage")]
        public double Percentage { get; set; }

        //[JsonProperty(PropertyName = "time")]
        //public DateTime Time { get; set; }
    }
}
