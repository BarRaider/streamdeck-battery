using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Battery.Internal
{
    internal class DeviceInfo
    {
        [JsonProperty(PropertyName = "name")]
        public String Name { get; set; }
    }
}
