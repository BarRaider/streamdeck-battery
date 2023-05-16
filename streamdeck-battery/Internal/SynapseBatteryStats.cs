using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Battery.Internal
{
    internal class SynapseBatteryStats
    {
        [JsonProperty(PropertyName = "updatedate")]
        public DateTime UpdateDate { get; set; }

        [JsonProperty(PropertyName = "devicename")]
        public string DeviceName { get; set; }

        [JsonProperty(PropertyName = "chargingstate")]
        public string ChargingState { get; set; }

        [JsonProperty(PropertyName = "percentage")]
        public int Percentage { get; set; }
    }
}
