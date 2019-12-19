using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Battery.Internal
{
    internal class ICueBatteryStats
    {
        [JsonProperty(PropertyName = "title")]
        public string Title { get; set; }

        [JsonProperty(PropertyName = "batteryLevel")]
        public string BatteryLevel { get; set; }

        [JsonProperty(PropertyName = "percentage")]
        public double Percentage { get; set; }
    }
}
