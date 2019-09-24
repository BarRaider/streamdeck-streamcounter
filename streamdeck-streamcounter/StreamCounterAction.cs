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

    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: CyberlightGames
    // frankdubbs - $5 tip
    //---------------------------------------------------

    public enum CounterFunctions
    {
        Add = 0,
        Subtract = 1,
        Multiply = 2,
        Divide = 3
    }

    [PluginActionId("com.barraider.streamcounter")]
    public class StreamCounterAction : PluginBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings
                {
                    CounterFileName = String.Empty,
                    TitlePrefix = String.Empty,
                    ShortPressCalculation = "0", // CounterFunctions.Add
                    LongPressCalculation  = "1", // CounterFunctions.Subtract
                    Increment = "1",
                    InitialValue = "0"
                };
                return instance;
            }

            [FilenameProperty]
            [JsonProperty(PropertyName = "counterFileName")]
            public string CounterFileName { get; set; }

            [JsonProperty(PropertyName = "titlePrefix")]
            public string TitlePrefix { get; set; }

            [JsonProperty(PropertyName = "shortPressCalculation")]
            public string ShortPressCalculation { get; set; }

            [JsonProperty(PropertyName = "longPressCalculation")]
            public string LongPressCalculation { get; set; }

            [JsonProperty(PropertyName = "increment")]
            public string Increment { get; set; }

            [JsonProperty(PropertyName = "initialValue")]
            public string InitialValue { get; set; }           
        }

        #region Private Members

        private delegate int CalculationFunction(int num1, int num2);

        private const int DECREASE_COUNTER_KEYPRESS_LENGTH = 600;
        private const int RESET_COUNTER_KEYPRESS_LENGTH = 2300;

        private readonly PluginSettings settings;
        private int counter = 0;
        private int incrementor = 1;
        private int initialValue = 0;
        private DateTime keyPressStart;
        private bool keyPressed = false;
        private CalculationFunction shortPressCalculation;
        private CalculationFunction longPressCalculation;
        
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
            InitializeSettings();
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
                counter = shortPressCalculation(counter, incrementor);
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
                    counter = longPressCalculation(counter, incrementor);
                    SaveCounterToFile();
                }
                else if (timeKeyWasPressed >= RESET_COUNTER_KEYPRESS_LENGTH) // Reset counter
                {
                    ResetCounter();
                }
            }
            await Connection.SetTitleAsync($"{settings.TitlePrefix ?? ""}{counter}");
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            string previousInitialValue = settings.InitialValue;
            Tools.AutoPopulateSettings(settings, payload.Settings);
            SaveSettings();
            LoadCounterFromFile();
            InitializeSettings();

            if (previousInitialValue != settings.InitialValue)
            {
                ResetCounter();
            }
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
            try
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"Saving value: {counter} to counter file: {settings.CounterFileName}");
                if (!String.IsNullOrWhiteSpace(settings.CounterFileName))
                {

                    File.WriteAllText(settings.CounterFileName, counter.ToString());
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error Saving value: {counter} to counter file: {settings.CounterFileName} : {ex}");
                Connection.ShowAlert();
                settings.CounterFileName = "ACCESS DENIED";
                SaveSettings();
            }
        }

        private Task SaveSettings()
        {
            return Connection.SetSettingsAsync(JObject.FromObject(settings));
        }

        private void InitializeSettings()
        {
            // Get user requested values
            shortPressCalculation = GetCalculationFunctionFromString(settings.ShortPressCalculation) ?? Add;
            longPressCalculation = GetCalculationFunctionFromString(settings.LongPressCalculation) ?? Subtract;

            if (Int32.TryParse(settings.Increment, out int incrementValue))
            {
                incrementor = incrementValue;
            }
            else // Invalid value in Increment field
            {
                settings.Increment = "1";
                SaveSettings();
            }

            if (Int32.TryParse(settings.InitialValue, out int value))
            {
                initialValue = value;
            }
            else // Invalid value in Increment field
            {
                settings.InitialValue = "0";
                SaveSettings();
            }
        }

        private CalculationFunction GetCalculationFunctionFromString(string functionString)
        {
            if (String.IsNullOrEmpty(functionString))
            {
                return null;
            }

            CounterFunctions counterFunction = (CounterFunctions)Enum.Parse(typeof(CounterFunctions), functionString);
            switch (counterFunction)
            {
                case CounterFunctions.Add:
                    return Add;
                case CounterFunctions.Subtract:
                    return Subtract;
                case CounterFunctions.Multiply:
                    return Multiply;
                case CounterFunctions.Divide:
                    return Divide;
            }
            return null;
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
                        ResetCounter();
                        break;
                }
            }
        }

        private void ResetCounter()
        {
            counter = initialValue;
            SaveCounterToFile();
        }

        #region Calculation Functions

        private int Add(int num1, int num2)
        {
            return num1 + num2;
        }

        private int Subtract(int num1, int num2)
        {
            return num1 - num2;
        }

        private int Multiply(int num1, int num2)
        {
            return num1 * num2;
        }

        private int Divide(int num1, int num2)
        {
            if (num2 == 0) // Prevent division by zero
            {
                return num1;
            }
            return num1 / num2;
        }


        #endregion

        #endregion
    }
}