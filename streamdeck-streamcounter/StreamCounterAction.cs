using BarRaider.SdTools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.StreamCounter
{
    // frankdubbs - $5 tip

    [PluginActionId("com.barraider.streamcounter")]
    public class StreamCounterAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings();
                instance.CounterFileName = String.Empty;
                return instance;
            }

            [FilenameProperty]
            [JsonProperty(PropertyName = "counterFileName")]
            public string CounterFileName { get; set; }
        }

        #region Private Members

        private const int DECREASE_COUNTER_KEYPRESS_LENGTH = 600;
        private const int RESET_COUNTER_KEYPRESS_LENGTH = 2300;

        private PluginSettings settings;
        private int counter = 0;
        private DateTime keyPressStart;
        private bool keyPressed = false;

        #endregion
        public StreamCounterAction(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                this.settings = PluginSettings.CreateDefaultSettings();
            }
            else
            {
                this.settings = payload.Settings.ToObject<PluginSettings>();
            }
            Connection.StreamDeckConnection.OnSendToPlugin += StreamDeckConnection_OnSendToPlugin;
            LoadCounterFromFile();
        }

        public override void Dispose()
        {
            Connection.StreamDeckConnection.OnSendToPlugin -= StreamDeckConnection_OnSendToPlugin;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public override void KeyPressed(KeyPayload payload)
        {
            // Used for long press
            keyPressStart = DateTime.Now;
            keyPressed = true;
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");
        }

        public override void KeyReleased(KeyPayload payload)
        {
            int timeKeyWasPressed = (int)(DateTime.Now - keyPressStart).TotalMilliseconds;
            if (timeKeyWasPressed < DECREASE_COUNTER_KEYPRESS_LENGTH) // Increase counter
            {
                counter++;
                SaveCounterToFile();
            }
            keyPressed = false;
        }

        public async override void OnTick()
        {
            if (keyPressed)
            {
                int timeKeyWasPressed = (int)(DateTime.Now - keyPressStart).TotalMilliseconds;
                if (timeKeyWasPressed >= DECREASE_COUNTER_KEYPRESS_LENGTH &&  timeKeyWasPressed < RESET_COUNTER_KEYPRESS_LENGTH) // Decrease counter
                {
                    counter--;
                    SaveCounterToFile();
                }
                else if (timeKeyWasPressed >= RESET_COUNTER_KEYPRESS_LENGTH) // Reset counter
                {
                    counter = 0;
                    SaveCounterToFile();
                }
            }
            await Connection.SetTitleAsync(counter.ToString());
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(settings, payload.Settings);
            SaveSettings();
            LoadCounterFromFile();
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload) { }

        #region Private Methods

        private void LoadCounterFromFile()
        {
            counter = 0;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Loading counter file: {settings.CounterFileName}");
            if (!String.IsNullOrWhiteSpace(settings.CounterFileName))
            {
                try
                {
                    if (!File.Exists(settings.CounterFileName))
                    {
                        SaveCounterToFile();
                    }

                    string text = File.ReadAllText(settings.CounterFileName);
                    if (int.TryParse(text, out counter)) // Try and read counter data from file and store in counter variable
                    {
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Loaded {counter} from file");
                    }
                    else
                    {
                        Logger.Instance.LogMessage(TracingLevel.ERROR, $"Invalid value in counter file: {text}");
                    }

                }
                catch (Exception ex)
                {
                    Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error loading counter from file: {ex}");
                }
            }
        }

        private void SaveCounterToFile()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Saving value: {counter} to counter file: {settings.CounterFileName}");
            if (!String.IsNullOrWhiteSpace(settings.CounterFileName))
            {

                File.WriteAllText(settings.CounterFileName, counter.ToString());
            }
        }

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }


        private void StreamDeckConnection_OnSendToPlugin(object sender, streamdeck_client_csharp.StreamDeckEventReceivedEventArgs<streamdeck_client_csharp.Events.SendToPluginEvent> e)
        {
            var payload = e.Event.Payload;
            if (Connection.ContextId != e.Event.Context)
            {
                return;
            }

            if (payload["property_inspector"] != null)
            {
                switch (payload["property_inspector"].ToString().ToLower())
                {
                    case "resetcounter":
                        counter = 0;
                        SaveCounterToFile();
                        break;
                }
            }
        }

        #endregion
    }
}