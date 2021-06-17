using BarRaider.SdTools;
using BarRaider.StreamCounter.Wrappers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BarRaider.StreamCounter.Actions
{

    //---------------------------------------------------
    //          BarRaider's Hall Of Fame
    // Subscriber: CyberlightGames
    // frankdubbs - $5 tip
    // Subscriber: Tek_Soup
    // Subscriber: Dualchart
    // Subscriber: ChaosZake (Gifted by Dualchart)
    // 100 Bits: fragglerocket
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
                    LongPressCalculation = "1", // CounterFunctions.Subtract
                    Increment = "1",
                    InitialValue = "0",
                    PlaySoundOnPress = false,
                    PlaybackDevice = String.Empty,
                    PlaybackDevices = null,
                    PlaySoundOnLongPressFile = String.Empty,
                    PlaySoundOnPressFile = String.Empty,
                    CounterPrefixFileName = String.Empty,
                    ClearFileOnReset = false
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

            [FilenameProperty]
            [JsonProperty(PropertyName = "counterPrefixFileName")]
            public string CounterPrefixFileName { get; set; }

            [JsonProperty(PropertyName = "playSoundOnPress")]
            public bool PlaySoundOnPress { get; set; }

            [JsonProperty(PropertyName = "playbackDevices")]
            public List<PlaybackDevice> PlaybackDevices { get; set; }

            [JsonProperty(PropertyName = "playbackDevice")]
            public string PlaybackDevice { get; set; }

            [FilenameProperty]
            [JsonProperty(PropertyName = "playSoundOnPressFile")]
            public string PlaySoundOnPressFile { get; set; }

            [FilenameProperty]
            [JsonProperty(PropertyName = "playSoundOnLongPressFile")]
            public string PlaySoundOnLongPressFile { get; set; }

            [JsonProperty(PropertyName = "clearFileOnReset")]
            public bool ClearFileOnReset { get; set; }  
        }

        #region Private Members

        private delegate int CalculationFunction(int num1, int num2);

        private const int DECREASE_COUNTER_KEYPRESS_LENGTH = 600;
        private const int RESET_COUNTER_KEYPRESS_LENGTH = 2300;
        private const string FILE_ERROR_MESSAGE = "ERROR SAVING";
        private const int COUNTER_REFRESH_TIME = 3000;

        private readonly PluginSettings settings;
        private int counter = 0;
        private int incrementor = 1;
        private int initialValue = 0;
        private DateTime keyPressStart;
        private bool keyPressed = false;
        private bool longKeyPressed = false;
        private CalculationFunction shortPressCalculation;
        private CalculationFunction longPressCalculation;
        private DateTime lastCounterUpdate;
        
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
            Connection.OnSendToPlugin += Connection_OnSendToPlugin;
            Connection.OnPropertyInspectorDidAppear += Connection_OnPropertyInspectorDidAppear;
            LoadCounterFromFile();
            InitializeSettings();
        }

        public override void Dispose()
        {
            Connection.OnPropertyInspectorDidAppear -= Connection_OnPropertyInspectorDidAppear;
            Connection.OnSendToPlugin -= Connection_OnSendToPlugin;
            Logger.Instance.LogMessage(TracingLevel.INFO, $"Destructor called");
        }

        public override void KeyPressed(KeyPayload payload)
        {
            // Used for long press
            keyPressStart = DateTime.Now;
            keyPressed = true;
            longKeyPressed = false;
            Logger.Instance.LogMessage(TracingLevel.INFO, "Key Pressed");
            LoadCounterFromFile();
        }

        public override void KeyReleased(KeyPayload payload)
        {
            int timeKeyWasPressed = (int)(DateTime.Now - keyPressStart).TotalMilliseconds;
            if (timeKeyWasPressed < DECREASE_COUNTER_KEYPRESS_LENGTH) // Increase counter
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, "Short key press");
                counter = shortPressCalculation(counter, incrementor);
                SaveCounterToFiles(false);
                PlaySoundOnPress(settings.PlaySoundOnPressFile);
            }
            keyPressed = false;
        }

        public async override void OnTick()
        {
            if (keyPressed)
            {
                int timeKeyWasPressed = (int)(DateTime.Now - keyPressStart).TotalMilliseconds;
                if (!longKeyPressed && timeKeyWasPressed >= DECREASE_COUNTER_KEYPRESS_LENGTH &&  timeKeyWasPressed < RESET_COUNTER_KEYPRESS_LENGTH) // Decrease counter
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, "Long key press");
                    longKeyPressed = true;
                    counter = longPressCalculation(counter, incrementor);
                    SaveCounterToFiles(false);
                    PlaySoundOnPress(settings.PlaySoundOnLongPressFile);
                }
                else if (timeKeyWasPressed >= RESET_COUNTER_KEYPRESS_LENGTH) // Reset counter
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Key pressed for {timeKeyWasPressed}, resetting");
                    ResetCounter();
                }
            }
            if ((DateTime.Now - lastCounterUpdate).TotalMilliseconds > COUNTER_REFRESH_TIME)
            {
                LoadCounterFromFile();
            }
            await Connection.SetTitleAsync($"{settings.TitlePrefix?.Replace(@"\n","\n") ?? ""}{counter}");
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            string previousInitialValue = settings.InitialValue;
            bool playSound = settings.PlaySoundOnPress;
            Tools.AutoPopulateSettings(settings, payload.Settings);
            SaveSettings();
            LoadCounterFromFile();
            InitializeSettings();

            if (previousInitialValue != settings.InitialValue)
            {
                Logger.Instance.LogMessage(TracingLevel.INFO, $"InitialValue setting was modified");
                ResetCounter();
            }

            if (playSound != settings.PlaySoundOnPress && settings.PlaySoundOnPress)
            {
                PropagatePlaybackDevices();
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
                    if (!File.Exists(settings.CounterFileName) || // If counter file doesn't exist
                        (!string.IsNullOrWhiteSpace(settings.CounterPrefixFileName) && !File.Exists(settings.CounterPrefixFileName))) // Prefix file doesn't exist
                    {
                        SaveCounterToFiles(false);
                    }

                    lastCounterUpdate = DateTime.Now;
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

        private void SaveCounterToFiles(bool isReset)
        {
            if (!SaveToFile(settings.CounterFileName, counter.ToString()))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Saving to counter file failed: {settings.CounterFileName}");
                settings.CounterFileName = FILE_ERROR_MESSAGE;
                SaveSettings();
            }

            // Clean out the prefix file if it's requested for a reset.
            string prefixText = $"{settings.TitlePrefix?.Replace(@"\n", "")}{counter}";
            if (isReset && settings.ClearFileOnReset)
            {
                prefixText = String.Empty;
            }

            if (!SaveToFile(settings.CounterPrefixFileName, prefixText))
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Saving to prefix file failed: {settings.CounterPrefixFileName}");
                settings.CounterPrefixFileName = FILE_ERROR_MESSAGE;
                SaveSettings();
            }
        }

        private bool SaveToFile(string fileName, string value)
        {
            try
            {
                if (!String.IsNullOrWhiteSpace(fileName))
                {
                    Logger.Instance.LogMessage(TracingLevel.INFO, $"Saving value: {value} to counter file: {fileName}");
                    File.WriteAllText(fileName, value);
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error Saving value: {value} to counter file: {fileName} : {ex}");
                Connection.ShowAlert();
                return false;
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

        private void PropagatePlaybackDevices()
        {
            settings.PlaybackDevices = new List<PlaybackDevice>();

            try
            {
                if (settings.PlaySoundOnPress)
                {

                    settings.PlaybackDevices = AudioUtils.Common.GetAllPlaybackDevices(true).Select(d => new PlaybackDevice() { ProductName = d }).OrderBy(p => p.ProductName).ToList();
                    SaveSettings();
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, $"Error propagating playback devices {ex}");
            }
        }
        private Task PlaySoundOnPress(string fileName)
        {
            return Task.Run(async () =>
            {
                if (!settings.PlaySoundOnPress)
                {
                    return;
                }

                if (String.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(settings.PlaybackDevice))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"PlaySoundOnPress called but File or Playback device are empty. File: {fileName} Device: {settings.PlaybackDevice}");
                    return;
                }

                if (!File.Exists(fileName))
                {
                    Logger.Instance.LogMessage(TracingLevel.WARN, $"PlaySoundOnPress called but file does not exist: {fileName}");
                    return;
                }

                Logger.Instance.LogMessage(TracingLevel.INFO, $"PlaySoundOnPress called. Playing {fileName} on device: {settings.PlaybackDevice}");
                await AudioUtils.Common.PlaySound(fileName, settings.PlaybackDevice);
            });
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
                default:
                    break;
            }
            return null;
        }

        private void Connection_OnSendToPlugin(object sender, SdTools.Wrappers.SDEventReceivedEventArgs<SdTools.Events.SendToPlugin> e)
        {
            var payload = e.Event.Payload;

            if (payload["property_inspector"] != null)
            {
                switch (payload["property_inspector"].ToString().ToLowerInvariant())
                {
                    case "resetcounter":
                        Logger.Instance.LogMessage(TracingLevel.INFO, $"Reset button pressed in PI");
                        ResetCounter();
                        break;
                    case "loadsavepicker":
                        string propertyName = (string)payload["property_name"];
                        string pickerTitle = (string)payload["picker_title"];
                        string pickerFilter = (string)payload["picker_filter"];
                        string fileName = PickersUtil.Pickers.SaveFilePicker(pickerTitle, null, pickerFilter);
                        if (!string.IsNullOrEmpty(fileName))
                        {
                            if (!PickersUtil.Pickers.SetJsonPropertyValue(settings, propertyName, fileName))
                            {
                                Logger.Instance.LogMessage(TracingLevel.ERROR, "Failed to save picker value to settings");
                            }
                            SaveSettings();
                        }
                        break;
                }
            }
        }

        private void ResetCounter()
        {
            Logger.Instance.LogMessage(TracingLevel.WARN, $"ResetCounter called");
            counter = initialValue;
            SaveCounterToFiles(true);
        }
        private void Connection_OnPropertyInspectorDidAppear(object sender, SdTools.Wrappers.SDEventReceivedEventArgs<SdTools.Events.PropertyInspectorDidAppear> e)
        {
            PropagatePlaybackDevices();
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