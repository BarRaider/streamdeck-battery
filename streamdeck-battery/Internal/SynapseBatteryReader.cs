using BarRaider.SdTools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using Newtonsoft.Json;

namespace Battery.Internal
{
    internal class SynapseReader
    {
        #region Private members

        private static SynapseReader instance = null;
        private static readonly object objLock = new object();

        private ConcurrentDictionary<string, SynapseBatteryStats> dicBatteryStats = new ConcurrentDictionary<string, SynapseBatteryStats>();

        private readonly int SYNAPSE_VERSION = -1;
        private const int REFRESH_TIMEOUT_MS = 10000;
        private readonly Timer tmrRefreshStats;

        private long lastMaxOffset = 0;

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

            // Attempt to determine the installed Razer Version

            // Check for Synapse 3
            if (Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"AppData\Local\Razer\Synapse3")))
            {
                SYNAPSE_VERSION = 3;
            }

            // Check for Synapse 4
            if (Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"AppData\Local\Razer\RazerAppEngine")))
            {
                SYNAPSE_VERSION = 4;
            }

            if (SYNAPSE_VERSION == -1)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} Constructor - Could not determine Synapse version");
                return;
            }

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
            var userProfileDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string fullLogPath;

            if (SYNAPSE_VERSION == 3)
            {
                // v3 is a single log file
                fullLogPath = Path.Combine(userProfileDir, @"AppData\Local\Razer\Synapse3\Log\Razer Synapse 3.log");
            }
            else
            {
                // v4's log rotation appears to follow the following rules:
                //    - When the log file reaches ~5MB, it is rotated to background-manager1.log
                //    - When background-manager1.log is full, it is rotated to background-manager2.log
                //    - When background-manager2.log is full, it is rotated to background-manager3.log
                //    - When background-manager3.log is full, it is rotated to background-manager4.log
                //    - When background-manager4.log is full, it is rotated to background-manager5.log, background-manager.log is deleted
                //    - When background-manager5.log is full:
                //        - background-manager1.log is deleted
                //        - background-manager2.log is renamed to background-manager1.log
                //        - background-manager3.log is renamed to background-manager2.log
                //        - background-manager4.log is renamed to background-manager3.log
                //        - background-manager5.log is renamed to background-manager4.log
                //        - A new background-manager5.log is created
                //    - The process repeats

                // First run is simple, we just take the latest file and start from there, so lets start with a search for files matching the pattern background-manager*.log
                var files = Directory.GetFiles(Path.Combine(userProfileDir, @"AppData\Local\Razer\RazerAppEngine\User Data\Logs"), "background-manager*.log");
                
                // if there are no files, log error and return
                if (files.Length == 0)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} RefreshStats - No V4 log files found in directory");
                    return;
                }
                
                // Get the latest file
                fullLogPath = files.OrderByDescending(f => f).FirstOrDefault();
            }

            // Now lets read it
            try
            {
                using (StreamReader reader = new StreamReader(new FileStream(fullLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
                {
                    if (reader.BaseStream.Length == lastMaxOffset)
                    {
                        // Filesize has not changed since previous check, idle.
                        return;
                    }

                    // If the current file is smaller than the last max offset, we have a new file.
                    if (reader.BaseStream.Length < lastMaxOffset)
                    {
                        // Reset the last max offset
                        lastMaxOffset = 0;
                    }

                    //seek to the last max offset
                    reader.BaseStream.Seek(lastMaxOffset, SeekOrigin.Begin);

                    //read out of the file until the EOF
                    string line = "";
                    while ((line = reader.ReadLine()) != null)
                    {
                        switch (SYNAPSE_VERSION)
                        {
                            case 3:
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
                                break;
                            case 4:
                                // V4 log file contains JSON objects, much cleaner to parse
                                if (line.Contains("info: opentab"))
                                {
                                    var dt = DateTime.Parse(line.Substring(1, 23));
                                    var json = line.Substring(line.IndexOf("{"));
                                    var jsonObj = JsonConvert.DeserializeObject<dynamic>(json);

                                    if (jsonObj?.hasBattery == true && jsonObj.powerStatus != null)
                                    {
                                        var device = new SynapseBatteryStats
                                        {
                                            DeviceName = jsonObj.name.en,
                                            UpdateDate = dt,
                                            Percentage = jsonObj.powerStatus.level,
                                            ChargingState = jsonObj.powerStatus.chargingStatus
                                        };

                                        dicBatteryStats[device.DeviceName] = device;
                                    }

                                }
                                break;
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
