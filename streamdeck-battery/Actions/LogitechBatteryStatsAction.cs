using BarRaider.SdTools;
using Battery.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Battery.Actions
{
    [PluginActionId("com.barraider.battery.ghub")]
    public class LogitechBatteryStatsAction : KeypadBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    Devices = null,
                    Device = String.Empty,
                    Title = String.Empty
                };
                return instance;
            }

            [JsonProperty(PropertyName = "devices")]
            public List<DeviceInfo> Devices { get; set; }

            [JsonProperty(PropertyName = "device")]
            public string Device { get; set; }

            [JsonProperty(PropertyName = "title")]
            public string Title { get; set; }
        }

        #region Private Members

        private readonly PluginSettings settings;

        private const int IMAGE_BATT_LOW = 0;
        private const int IMAGE_BATT_MID = 1;
        private const int IMAGE_BATT_FULL = 2;
        private readonly string[] imageFiles = { @"images\battred.png", @"images\battyellow.png", @"images\battgreen.png" };

        private Image lowImage = null;
        private Image midImage = null;
        private Image fullImage = null;


        #endregion
        public LogitechBatteryStatsAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }

            Connection.StreamDeckConnection.OnPropertyInspectorDidAppear += StreamDeckConnection_OnPropertyInspectorDidAppear;
            PrefetchImages();
        }

        public override void Dispose()
        {
            Connection.StreamDeckConnection.OnPropertyInspectorDidAppear -= StreamDeckConnection_OnPropertyInspectorDidAppear;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public override void KeyPressed(KeyPayload payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");
        }

        public override void KeyReleased(KeyPayload payload) { }

        public async override void OnTick()
        {
            var stats = GHubReader.Instance.GetBatteryStats(settings.Device);
            if (stats == null)
            {
                await Connection.SetImageAsync((String)null);
                return;
            }

            string title = $"{(int)stats.Percentage}%{(stats.IsCharging ? " ⚡" : "")}";
            if (!string.IsNullOrEmpty(settings.Title))
            {
                title += $"\n{settings.Title.Replace(@"\n", "\n")}";
            }

            await Connection.SetTitleAsync(title);
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
            Tools.AutoPopulateSettings(settings, payload.Settings);
            SaveSettings();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private void StreamDeckConnection_OnPropertyInspectorDidAppear(object sender, BarRaider.SdTools.Wrappers.SDEventReceivedEventArgs<BarRaider.SdTools.Communication.SDEvents.PropertyInspectorDidAppearEvent> e)
        {
            settings.Devices = GHubReader.Instance.GetAllDevices();
            SaveSettings();
        }

        private void PrefetchImages()
        {
            lowImage = Image.FromFile(imageFiles[IMAGE_BATT_LOW]);
            midImage = Image.FromFile(imageFiles[IMAGE_BATT_MID]);
            fullImage = Image.FromFile(imageFiles[IMAGE_BATT_FULL]);
        }

        #endregion
    }
}