using BarRaider.SdTools;
using Battery.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Drawing;
using System.Threading.Tasks;

namespace Battery.Actions
{
    [PluginActionId("com.barraider.battery.icue")]
    public class ICueBatteryStatsAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    DeviceName = String.Empty
                };
                return instance;
            }

            [JsonProperty(PropertyName = "deviceName")]
            public string DeviceName { get; set; }
        }

        #region Private Members
        private const string CHARGING_TEXT = "Charging";
        private readonly PluginSettings settings;

        private const int IMAGE_BATT_LOW = 0;
        private const int IMAGE_BATT_MID = 1;
        private const int IMAGE_BATT_FULL = 2;
        private readonly string[] imageFiles = { @"images\battred.png", @"images\battyellow.png", @"images\battgreen.png" };

        private Image lowImage = null;
        private Image midImage = null;
        private Image fullImage = null;


        #endregion
        public ICueBatteryStatsAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }

            PrefetchImages();
            RegisterDeviceName();
        }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick()
        {
            if (String.IsNullOrEmpty(settings.DeviceName))
            {
                return;
            }

            var stats = ICueReader.Instance.GetBatteryStats(settings.DeviceName);
            if (stats == null)
            {
                await Connection.SetImageAsync((String)null);
                return;
            }

            string batteryLevel = stats.BatteryLevel == CHARGING_TEXT ? "⚡" : stats.BatteryLevel;
            await Connection.SetTitleAsync($"{batteryLevel}");
            if (stats.Percentage >= 75)
            {
                await Connection.SetImageAsync(fullImage);
            }
            else if (stats.Percentage >= 25)
            {
                await Connection.SetImageAsync(midImage);
            }
            else
            {
                await Connection.SetImageAsync(lowImage);
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            string previousDeviceName = settings.DeviceName;
            Tools.AutoPopulateSettings(settings, payload.Settings);
            if (previousDeviceName != settings.DeviceName)
            {
                RegisterDeviceName();
            }

            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private void PrefetchImages()
        {
            lowImage = Image.FromFile(imageFiles[IMAGE_BATT_LOW]);
            midImage = Image.FromFile(imageFiles[IMAGE_BATT_MID]);
            fullImage = Image.FromFile(imageFiles[IMAGE_BATT_FULL]);
        }

        private void RegisterDeviceName()
        {
            if (!String.IsNullOrEmpty(settings.DeviceName))
            {
                ICueReader.Instance.RegisterDeviceName(settings.DeviceName);
            }
        }

        #endregion
    }
}