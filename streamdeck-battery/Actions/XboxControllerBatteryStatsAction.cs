using BarRaider.SdTools;
using Battery.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using XInput.Wrapper;
using static XInput.Wrapper.X.Gamepad.Battery;

namespace Battery.Actions
{
    [PluginActionId("com.barraider.battery.xbox")]
    public class XboxControllerBatteryStatsAction : KeypadBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    Device = String.Empty,
                    Title = string.Empty
                };
                return instance;
            }

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
        public XboxControllerBatteryStatsAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
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
            ChargeLevel? level = null;
            var title = "Not\nConnected";

            if (String.IsNullOrEmpty(settings.Device))
            {
                return;
            }

            switch (settings.Device)
            {
                case "Gamepad 1":
                    X.Gamepad_1.Update();
                    if (X.Gamepad_1.IsConnected)
                    {
                        X.Gamepad_1.UpdateBattery();
                        level = X.Gamepad_1.BatteryInfo.ChargeLevel;
                    }
                    break;
                case "Gamepad 2":
                    X.Gamepad_2.Update();
                    if (X.Gamepad_2.IsConnected)
                    {
                        X.Gamepad_2.UpdateBattery();
                        level = X.Gamepad_2.BatteryInfo.ChargeLevel;
                    }
                    break;
                case "Gamepad 3":
                    X.Gamepad_3.Update();
                    if (X.Gamepad_3.IsConnected)
                    {
                        X.Gamepad_3.UpdateBattery();
                        level = X.Gamepad_3.BatteryInfo.ChargeLevel;
                    }
                    break;
                case "Gamepad 4":
                    X.Gamepad_4.Update();
                    if (X.Gamepad_4.IsConnected)
                    {
                        X.Gamepad_4.UpdateBattery();
                        level = X.Gamepad_4.BatteryInfo.ChargeLevel;
                    }
                    break;
                default:
                    await Connection.SetImageAsync((string)null);
                    return;
            }

            if(level.HasValue)
            {
                title = level.Value.ToString();
            }
            else
            {
                await Connection.SetImageAsync((string)null);
            }

            if (!string.IsNullOrEmpty(settings.Title))
            {
                title += $"\n{settings.Title.Replace(@"\n", "\n")}";
            }
            await Connection.SetTitleAsync(title);

            switch (level)
            {
                case ChargeLevel.Full:
                    await Connection.SetImageAsync(fullImage);
                    break;
                case ChargeLevel.Medium:
                    await Connection.SetImageAsync(midImage);
                    break;
                case ChargeLevel.Low:
                    await Connection.SetImageAsync(lowImage);
                    break;
                case ChargeLevel.Empty:
                    await Connection.SetImageAsync((string)null);
                    break;
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

        private void PrefetchImages()
        {
            lowImage = Image.FromFile(imageFiles[IMAGE_BATT_LOW]);
            midImage = Image.FromFile(imageFiles[IMAGE_BATT_MID]);
            fullImage = Image.FromFile(imageFiles[IMAGE_BATT_FULL]);
        }

        #endregion
    }
}