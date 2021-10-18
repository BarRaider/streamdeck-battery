using BarRaider.SdTools;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;

namespace Battery.Internal
{
    internal class GHubReader
    {
        #region Private members
        private const string GHUB_SETTINGS_FILE = @"LGHUB\settings.db";
        private const string GHUB_BATTERY_SECTION = "percentage";
        private const string GHUB_BATTERY_WARNING_SECTION = "warning";

        private static GHubReader instance = null;
        private static readonly object objLock = new object();

        private readonly Timer tmrRefreshStats;
        private readonly string GHUB_FULL_PATH;
        private Dictionary<string, GHubBatteryStats> dicBatteryStats;

        #endregion

        #region Constructors

        public static GHubReader Instance
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
                        instance = new GHubReader();
                    }
                    return instance;
                }
            }
        }

        private GHubReader()
        {
            GHUB_FULL_PATH = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), GHUB_SETTINGS_FILE);

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

        public List<DeviceInfo> GetAllDevices()
        {
            if (dicBatteryStats == null || dicBatteryStats.Count == 0)
            {
                RefreshStats();
            }
            return dicBatteryStats.Keys.Select(s => new DeviceInfo() { Name = s }).ToList();
        }

        public GHubBatteryStats GetBatteryStats(string deviceName)
        {
            if (dicBatteryStats == null || !dicBatteryStats.ContainsKey(deviceName))
            {
                return null;
            }

            return dicBatteryStats[deviceName];
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
                dicBatteryStats = new Dictionary<string, GHubBatteryStats>();
                if (!File.Exists(GHUB_FULL_PATH))
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"RefreshStats Error: Cannot find settings file: {GHUB_FULL_PATH}");
                    tmrRefreshStats.Stop();
                }

                //var settings = JObject.Parse(File.ReadAllText(GHUB_FULL_PATH));
                var settings = ReadSettingsDB(GHUB_FULL_PATH);
                if (settings == null)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} RefreshStats: Could not read G HUB settings");
                    return;
                }
                var properties = settings.Properties().Where(p => p.Name.Contains("battery")).ToList();
                foreach (var property in properties)
                {
                    string[] splitName = property.Name.Split('/');
                    if (splitName.Length != 3)
                    {
                        continue;
                    }

                    if (splitName[2] != GHUB_BATTERY_SECTION && splitName[2] != GHUB_BATTERY_WARNING_SECTION)
                    {
                        continue;
                    }
                    var stats = property.Value.ToObject<GHubBatteryStats>();

                    if (dicBatteryStats.ContainsKey(splitName[1]) && splitName[2] == GHUB_BATTERY_WARNING_SECTION)
                    {
                        continue;
                    }

                    dicBatteryStats[splitName[1]] = stats;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"RefreshStats Error: Failed to parse json: {ex}");
                tmrRefreshStats.Stop();
            }
        }

        private JObject ReadSettingsDB(string fileName)
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={fileName}"))
                {
                    connection.Open();

                    string sql = "SELECT FILE FROM DATA ORDER BY _id DESC";
                    using (SqliteCommand command = new SqliteCommand(sql, connection))
                    {
                        using (SqliteDataReader reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return JObject.Parse(reader.GetString(0));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"{this.GetType()} ReadSettingsDB Exception: {ex}");
            }
            return null;
        }

        #endregion
    }
}
