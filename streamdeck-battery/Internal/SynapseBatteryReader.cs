using BarRaider.SdTools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;

namespace Battery.Internal
{
    internal class SynapseReader
    {
        #region Private members

        private static SynapseReader instance = null;
        private static readonly object objLock = new object();

        private ConcurrentDictionary<string, SynapseBatteryStats> dicBatteryStats = new ConcurrentDictionary<string, SynapseBatteryStats>();

        private const string SYNAPSE_LOG_FILE_ENDING = @"AppData\Local\Razer\Synapse3\Log\Razer Synapse 3.log";
        private readonly string SYNAPSE_FULL_PATH;
        private const int REFRESH_TIMEOUT_MS = 10000;
        private readonly Timer tmrRefreshStats;

        #endregion

        #region Constructors

        public static SynapseReader Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                lock (objLock)
                {
                    if (instance == null)
                    {
                        instance = new SynapseReader();
                    }
                    return instance;
                }
            }
        }

        private SynapseReader()
        {
            var userProfileDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            SYNAPSE_FULL_PATH = Path.Combine(userProfileDir, SYNAPSE_LOG_FILE_ENDING);

            tmrRefreshStats = new Timer
            {
                Interval = REFRESH_TIMEOUT_MS
            };
            tmrRefreshStats.Elapsed += TmrRefreshStats_Elapsed;
            tmrRefreshStats.Start();
            RefreshStats();
        }

        #endregion

        #region Public Methods

        public List<DeviceInfo> GetAllDevices()
        {

            if (dicBatteryStats == null || dicBatteryStats.Count == 0)
            {
                RefreshStats();
            }
            return dicBatteryStats.Keys.Select(s => new DeviceInfo() { Name = s }).ToList();
        }

        public SynapseBatteryStats GetBatteryStats(string deviceName)
        {
            if (dicBatteryStats == null || !dicBatteryStats.TryGetValue(deviceName, out SynapseBatteryStats stats))
            {
                return null;
            }

            return stats;
        }


        #endregion

        #region Private Methods

        private void TmrRefreshStats_Elapsed(object sender, ElapsedEventArgs e)
        {
            RefreshStats();
        }

        private void RefreshStats()
        {
            if (!File.Exists(SYNAPSE_FULL_PATH))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} ReadLogFile - File not found {SYNAPSE_FULL_PATH}");
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(new FileStream(SYNAPSE_FULL_PATH, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    //start at the end of the file
                    long lastMaxOffset = reader.BaseStream.Length;

                    //if the file size has not changed, idle
                    if (reader.BaseStream.Length == lastMaxOffset)
                    {
                        return;
                    }

                    //seek to the last max offset
                    reader.BaseStream.Seek(lastMaxOffset, SeekOrigin.Begin);

                    //read out of the file until the EOF
                    string line = "";
                    while ((line = reader.ReadLine()) != null)
                    {
                        // The log file should contain entries like:
                        //   2023 - 03 - 20 18:34:38.4929 INFO 1 Battery Get By Device Handle:
                        //   Name: Razer BlackWidow
                        //   Product_ID: 602(0x25A)
                        //   Edition_ID: 0(0x0)
                        //   Vendor_ID: 5426(0x1532)
                        //   Layout: 6(0x6)
                        //   Serial_Number: IO2204F46400041
                        //   Firmware_Version: 
                        //   Handle: 481147576(0x1CADBAB8)
                        //   Type: 2
                        //   SKU_ID: SKU
                        //   RegionId: 0
                        //   Feature Name: Battery
                        //   Feature Id: 1315d015 - e350 - 4541 - 9658 - a6030fb62776
                        //   Battery Percentage: 95
                        //   Battery State: Charging
                        //   Device on Dock State: Invalid
                        //   Charging StatusInvalid
                        if (line.Contains("INFO 1 Battery Get By Device Handle"))
                        {
                            var dt = DateTime.Parse(line.Substring(0, 24));

                            var deviceName = reader.ReadLine();
                            deviceName = deviceName?.Substring(deviceName.LastIndexOf(':') + 2);

                            var device = new SynapseBatteryStats { DeviceName = deviceName };

                            // Update the device properties
                            device.UpdateDate = dt;

                            var gotEverything = false;
                            while (gotEverything == false)
                            {
                                line = reader.ReadLine();
                                if (line.Contains("Battery Percentage:"))
                                {
                                    device.Percentage = Convert.ToInt32(line.Substring(line.LastIndexOf(':') + 2));
                                }

                                if (line.Contains("Battery State:"))
                                {
                                    device.ChargingState = line.Substring(line.LastIndexOf(':') + 2);
                                    gotEverything = true;
                                }
                            }

                            dicBatteryStats[deviceName] = device;
                        }
                    }

                    //update the last max offset
                    lastMaxOffset = reader.BaseStream.Position;

                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} ReadLogFile Exception: {ex}");
            }
        }
    }
    #endregion
}
