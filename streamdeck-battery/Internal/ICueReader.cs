using BarRaider.SdTools;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;

namespace Battery.Internal
{
    internal class ICueReader
    {
        #region Private members
        private const string BATTERY_STATUS_PREFIX = "Battery Status:";

        private static ICueReader instance = null;
        private static readonly object objLock = new object();

        private readonly Timer tmrRefreshStats;
        private Dictionary<string, ICueBatteryStats> dicBatteryStats;
        private List<string> deviceNames;

        #endregion

        #region Constructors

        public static ICueReader Instance
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
                        instance = new ICueReader();
                    }
                    return instance;
                }
            }
        }

        private ICueReader()
        {
            dicBatteryStats = new Dictionary<string, ICueBatteryStats>();
            deviceNames = new List<string>();

            tmrRefreshStats = new Timer
            {
                Interval = 10000
            };
            tmrRefreshStats.Elapsed += TmrRefreshStats_Elapsed;
            tmrRefreshStats.Start();
            RefreshStats();
        }

        #endregion

        #region Public Methods

        public ICueBatteryStats GetBatteryStats(string deviceName)
        {
            string device = deviceName.ToLower();
            if (dicBatteryStats == null || !dicBatteryStats.ContainsKey(device))
            {
                return null;
            }

            return dicBatteryStats[device];
        }

        public bool RegisterDeviceName(string deviceName)
        {
            string device = deviceName.ToLowerInvariant();
            if (!deviceName.Contains(device))
            {
                deviceNames.Add(device);
                RefreshStats();
            }

            return true;
        }

        #endregion

        #region Private Methods

        private void TmrRefreshStats_Elapsed(object sender, ElapsedEventArgs e)
        {
            RefreshStats();
        }

        private void RefreshStats()
        {
            try
            {
                if (deviceNames.Count == 0)
                {
                    return;
                }
                var titles = ToolbarScanner.ScanToolbarButtons();

                foreach (string title in titles)
                {
                    if (!title.Contains(BATTERY_STATUS_PREFIX))
                    {
                        continue;
                    }


                    // Current title has the Battery Status prefix inside.
                    string lowercaseTitle = title.ToLowerInvariant();
                    string deviceName = deviceNames.Where(name => lowercaseTitle.Contains(name)).FirstOrDefault();

                    if (!String.IsNullOrEmpty(deviceName))
                    {
                        var position = title.LastIndexOf(BATTERY_STATUS_PREFIX);
                        position += BATTERY_STATUS_PREFIX.Length;
                        string batteryLevel = title.Substring(position).Trim();
                        dicBatteryStats[deviceName] = new ICueBatteryStats() { Title = title, BatteryLevel = batteryLevel, Percentage = CalculatePercentage(batteryLevel) };
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"RefreshStats Error: {ex}");
                tmrRefreshStats.Stop();
            }
        }

        private double CalculatePercentage(string batteryLevel)
        {
            switch (batteryLevel.ToLowerInvariant())
            {
                case "charging":
                case "full":
                    return 100;
                case "high":
                    return 80;
                case "medium":
                    return 60;
                case "low":
                    return 24;
                case "critical":
                    return 5;
                default:
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"Invalid iCue percentage: {batteryLevel}");
                    return 0;                  
            }
        }

        #endregion
    }
}
