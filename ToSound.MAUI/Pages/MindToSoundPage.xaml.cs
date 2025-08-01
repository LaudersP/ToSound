using System;
using System.Collections;
using System.Diagnostics;
using CommunityToolkit.Maui.Storage;
using CortexAccess;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Text;
using System.Threading.Tasks;

namespace ToSound.Pages
{
    public partial class MindToSoundPage : ContentPage
    {
        // UI variables
        private uint _numSamplingsTransmitted = 0;
        private uint _numSamplingsSaved = 0;
        private uint _numRecordings = 0;
        private DateTime _transmissionStartTime;
        private DateTime _recordingStartTime;
        Color _green = Color.FromArgb("#00AB66");
        Color _red = Color.FromArgb("#CF142B");

        // Variables
        private string? _volunteerName = null;
        private string? _sessionID = null;
        private readonly int _maxPathLength = 45;
        private string _saveLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ToSound");
        private FileStream? _fs = null;
        private OSC? _osc = null;
        DataStreamExample dse = new();
        private ArrayList? _fileHeader = null;
        private readonly string[] _bands = { "theta", "alpha", "betaL", "betaH", "gamma" };
        private readonly string[] _sensors = { "AF3", "F7", "F3", "FC5", "T7", "P7", "O1", "O2", "P8", "T8", "FC6", "F4", "F8", "AF4" };
        private List<bool> _bandFlagsList = [];
        private List<bool> _sensorFlagsList = [];
        private static Mutex _transmissionMutex = new();
        private static Mutex _statsVariableMutex = new();
        private Thread? _statisticsUpdaterThread = null;

        // Flags
        private bool _isHeadsetConnected = false;
        private bool _transmitTheta = false;
        private bool _transmitAlpha = false;
        private bool _transmitBetaH = false;
        private bool _transmitBetaL = false;
        private bool _transmitGamma = false;
        private bool _transmitAF3 = false;
        private bool _transmitF7 = false;
        private bool _transmitF3 = false;
        private bool _transmitFC5 = false;
        private bool _transmitT7 = false;
        private bool _transmitP7 = false;
        private bool _transmitO1 = false;
        private bool _transmitAF4 = false;
        private bool _transmitF8 = false;
        private bool _transmitF4 = false;
        private bool _transmitFC6 = false;
        private bool _transmitT8 = false;
        private bool _transmitP8 = false;
        private bool _transmitO2 = false;
        private bool _isTransmittingData = false;
        private bool _isRecording = false;

        public MindToSoundPage()
        {
            InitializeComponent();

            // Check if the save location directory exists
            if(!Directory.Exists(_saveLocation))
            {
                // Create the directory for the save location
                Directory.CreateDirectory(_saveLocation);
            }

            // Set the save location path 
            DisplayPath(_saveLocation);

            // Set the number of samplings transmitted
            _statsVariableMutex.WaitOne();
            TotalSamplingsTransmitted.Text = _numSamplingsTransmitted.ToString();
            _statsVariableMutex.ReleaseMutex();

            // Set the initial headset status
            DisplayHeadsetStatus();

            // Set the number of recordings saved
            _statsVariableMutex.WaitOne();
            RecordingsSaved.Text = _numRecordings.ToString();
            _statsVariableMutex.ReleaseMutex();

            // Set the number of samplings saved
            _statsVariableMutex.WaitOne();
            TotalSamplingsSaved.Text = _numSamplingsSaved.ToString();
            _statsVariableMutex.ReleaseMutex();
        }

        public async void OnSelectLocationClicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("Select Location Clicked...");

            // Let user select folder
            FolderPickerResult folder;
            try
            {
                folder = await FolderPicker.PickAsync(default);
            }
            catch
            {
                // Pass: Error is thrown when user cancels file selection
                return;
            }
            
            // Ensure folder picking was successfully
            if(folder.IsSuccessful)
            {
                Debug.WriteLine($"\tFolder \'{folder.Folder.Name}\' was picked!");
                Debug.WriteLine($"\tPath: {folder.Folder.Path}");

                // Update the location
                _saveLocation = folder.Folder.Path;
                DisplayPath(_saveLocation);
            }
        }

        public void OnThetaClicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("Theta Clicked...");

            // Set the theta transmission flag
            _transmitTheta = ThetaCheckBox.IsChecked;
        }

        public void OnAlphaClicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("Alpha Clicked...");

            // Set the alpha transmission flag
            _transmitAlpha = AlphaCheckBox.IsChecked;
        }

        public void OnBetaLClicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("BetaL Clicked...");

            // Set the BetaL transmission flag
            _transmitBetaL = BetaLCheckBox.IsChecked;
        }

        public void OnBetaHClicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("BetaH Clicked...");

            // Set the BetaH transmission flag
            _transmitBetaH = BetaHCheckBox.IsChecked;
        }

        public void OnGammaClicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("Gamma Clicked...");

            // Set the gamma transmission flag
            _transmitGamma = GammaCheckBox.IsChecked;
        }

        public void OnAF3Clicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("AF3 Clicked...");

            // Set the AF3 transmission flag
            _transmitAF3 = AF3CheckBox.IsChecked;
        }

        public void OnF7Clicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("F7 Clicked...");

            // Set the F7 transmission flag
            _transmitF7 = F7CheckBox.IsChecked;
        }

        public void OnF3Clicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("F3 Clicked...");

            // Set the F3 transmission flag
            _transmitF3 = F3CheckBox.IsChecked;
        }

        public void OnFC5Clicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("FC5 Clicked...");

            // Set the FC5 transmission flag
            _transmitFC5 = FC5CheckBox.IsChecked;
        }

        public void OnT7Clicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("T7 Clicked...");

            // Set the T7 transmission flag
            _transmitT7 = T7CheckBox.IsChecked;
        }

        public void OnP7Clicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("P7 Clicked...");

            // Set the P7 transmission flag
            _transmitP7 = P7CheckBox.IsChecked;
        }

        public void OnO1Clicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("O1 Clicked...");

            // Set the O1 transmission flag
            _transmitO1 = O1CheckBox.IsChecked;
        }

        public void OnAF4Clicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("AF4 Clicked...");

            // Set the AF4 transmission flag
            _transmitAF4 = AF4CheckBox.IsChecked;
        }

        public void OnF8Clicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("F8 Clicked...");

            // Set the F8 transmission flag
            _transmitF8 = F8CheckBox.IsChecked;
        }

        public void OnF4Clicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("F4 Clicked...");

            // Set the F4 transmission flag
            _transmitF4 = F4CheckBox.IsChecked;
        }

        public void OnFC6Clicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("FC6 Clicked...");

            // Set the FC6 transmission flag
            _transmitFC6 = FC6CheckBox.IsChecked;
        }

        public void OnT8Clicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("T8 Clicked...");

            // Set the T8 transmission flag
            _transmitT8 = T8CheckBox.IsChecked;
        }

        public void OnP8Clicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("P8 Clicked...");

            // Set the P8 transmission flag
            _transmitP8 = P8CheckBox.IsChecked;
        }

        public void OnO2Clicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("O2 Clicked...");

            // Set the O2 transmission flag
            _transmitO2 = O2CheckBox.IsChecked;
        }

        public async void OnHeadsetClicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("Headset Clicked...");
            uint attempt_count = 0;

            // Check if we are disconnecting the headset
            if (_isHeadsetConnected)
            {
                // Disconnect from the headset
                Debug.WriteLine("\tDisconnecting from headset");

                // Unsubscribe from the stream
                dse.UnSubscribe();

                // Disconnect from the headset
                while (_isHeadsetConnected && attempt_count < 5)
                {
                    // Attempt to close the session, checking if successful
                    if(dse.CloseSession())
                    {
                        _isHeadsetConnected = false;
                        break;
                    }

                    // Unable to disconnect
                    attempt_count++;
                }

                // Check if we successfully disconnected from the headset
                if(!_isHeadsetConnected)
                {
                    Debug.WriteLine("\tSuccessfully disconnected from headset");

                    // Lower connection flag
                    _isHeadsetConnected = false;

                    // Enable headset ID field
                    HeadsetId.IsEnabled = true;

                    // Update connection status to disconnected
                    DisplayHeadsetStatus();

                    // Change button to connect
                    HeadsetConnectButton.Text = "Connect";

                    // Disable the send button
                    TransmissionButton.IsEnabled = false;
                }
                // Unsuccessful
                else
                {
                    Debug.WriteLine("\tUnable to disconnect from headset");
                    await DisplayAlert("ERROR", "Unable to disconnect from the headset, please retry", "Okay");
                    return;
                }
            }
            // Connecting a headset
            else
            {
                // Disable the connection button 
                HeadsetConnectButton.IsEnabled = false;

                // Disable the headdset ID input
                HeadsetId.IsEnabled = false;

                // Get the headset ID
                string headset_id = HeadsetId.Text;

                // Check if user specified a headset
                if(headset_id is not null )
                {
                    // Capatilize the headset ID
                    headset_id = headset_id.ToUpper();

                    // Check if only serial number was given
                    if (!headset_id.Contains("EPOCX-"))
                    {
                        headset_id = "EPOCX-" + headset_id;
                    }
                }

                // Add the POW datastream
                dse.AddStreams("pow");
                dse.OnSubscribed += SubscribedOK;
                dse.OnBandPowerDataReceived += OnBandPowerOK;

                // Connect to the headset
                Debug.WriteLine($"\tConnecting to headset {headset_id}...");
                while(!_isHeadsetConnected && attempt_count < 5)
                {
                    // Attempt to connect, checking if it was successful
                    if(dse.Start("", false, headset_id))
                    {
                        _isHeadsetConnected = true;
                        break;
                    }

                    // Unable to connect
                    attempt_count++;
                }

                // Check if we successfully connected to the headset
                if (_isHeadsetConnected)
                {
                    // Delay to ensure connection has finished starting up
                    await Task.Delay(1000);

                    // Update the connection status
                    Debug.WriteLine("\tSuccessfully connected to headset.");
                    DisplayHeadsetStatus();

                    // Update the button to be a disconnect button
                    HeadsetConnectButton.Text = "Disconnect";

                    // Enable the send data button 
                    TransmissionButton.IsEnabled = true;
                }
                else
                {
                    // Display the error
                    Debug.WriteLine("\tUnable to connect to the headset");
                    await DisplayAlert("ERROR", "Unable to connect to the headset, please retry", "Okay");
                    
                    // Enable the headset ID entry
                    HeadsetId.IsEnabled = true;
                }

                // Enable the connection button
                HeadsetConnectButton.IsEnabled = true;
            }
        }

        public async void OnTransmissionClicked(Object sender, EventArgs e)
        {
            Debug.WriteLine("Transmission Clicked...");

            // Check if we are already transmitting data
            if (_isTransmittingData)
            {
                Debug.WriteLine("\tStopping transmission");

                // Lower the transmission flag
                _isTransmittingData = false;

                // Join the stats updating thread
                if (_statisticsUpdaterThread is not null)
                {
                    _statisticsUpdaterThread.Join();
                    _statisticsUpdaterThread = null;
                }

                // Reset the samplings transmitted variable
                _numSamplingsTransmitted = 0;

                // Zero the OSC channels
                ZeroOSCChannels();

                // Clear the bands flags list
                _bandFlagsList.Clear();

                // Clear the sensors flags list
                _sensorFlagsList.Clear();

                // Check if there is a OSC instance
                if(_osc is not null)
                {
                    // Close the OSC instance
                    _osc.Close();
                }

                // Enable the OSC entries
                ToggleOSCEntries();

                // Enable the disconnect button
                HeadsetConnectButton.IsEnabled = true;

                // Enable wave frequency checkboxs
                ToggleWaveCheckboxs();

                // Enable sensor checkboxs
                ToggleSensorCheckboxs();

                // Change button to start
                TransmissionButton.BackgroundColor = _green;
                TransmissionButton.Text = "Send";

                // Disable record button
                RecordButton.IsEnabled = false;
            }
            // Not transmitting data
            else
            {
                Debug.WriteLine("\tStarting transmission");

                // Pull the OSC IP address
                string? ipAddress = OSCIpAddress.Text;

                // Check if user wants default ip address
                if(ipAddress is null)   
                {
                    Debug.WriteLine("\tUsing Default Address: \'127.0.0.1\'");
                    ipAddress = "127.0.0.1";
                }
                else
                {
                    // Validate IP address
                    if (!System.Net.IPAddress.TryParse(ipAddress, out _))
                    {
                        Debug.WriteLine($"\tInvalid IP Address: \'{ipAddress}\'");
                        await DisplayAlert("ERROR", "Invalid IP address, please enter a valid IP", "Okay");
                        return;
                    }
                    
                    Debug.WriteLine($"\tValidated IP Address: \'{ipAddress}\'");
                }

                // Pull the OSC Port Number
                string? portNumber = OSCPortNum.Text;

                // Check if user wants default port number
                if(portNumber is null)
                {
                    Debug.WriteLine("\tUsing Default Port: \'55555\'");
                    portNumber = "55555";
                }
                else
                {
                    // Validate IP address
                    if (ushort.TryParse(portNumber, out ushort port))
                    {
                        // Check that the port number is in the recommended user port range
                        if (port < 1024 || port > 49151)
                        {
                            Debug.WriteLine("\tPort is outside the recommended user ports: 1024-49151");
                            bool answer = await DisplayAlert("WARNING", "Port number is outside the recommended ports: 1024-49151", "Continue", "Cancel");
                            
                            if (!answer)
                            {
                                return;
                            }
                        }

                        Debug.WriteLine($"\tValidated Port Number: \'{portNumber}\'");
                    }
                    else
                    {
                        Debug.WriteLine($"\tInvalid Port Number: \'{portNumber}\'");
                        await DisplayAlert("ERROR", "Invalid Port, please enter a valid port (recommended: 1024-49151)", "Okay");
                        return;
                    }
                }

                // Disable OSC entries
                ToggleOSCEntries();

                // Create a OSC instance
                _osc = new(ipAddress, Convert.ToInt32(portNumber));
                Debug.WriteLine("\tOpened OSC Port");

                // Disable the disconnect headset button
                HeadsetConnectButton.IsEnabled = false;

                // Disable wave frequency checkboxs
                ToggleWaveCheckboxs();

                // Disable sensor checkboxs
                ToggleSensorCheckboxs();

                // Assign the band flags to the band flags list
                _bandFlagsList.Add(_transmitTheta);
                _bandFlagsList.Add(_transmitAlpha);
                _bandFlagsList.Add(_transmitBetaL);
                _bandFlagsList.Add(_transmitBetaH);
                _bandFlagsList.Add(_transmitGamma);

                // Assign the sensor flags to the sensor flags list
                _sensorFlagsList.Add(_transmitAF3);
                _sensorFlagsList.Add(_transmitF7);
                _sensorFlagsList.Add(_transmitF3);
                _sensorFlagsList.Add(_transmitFC5);
                _sensorFlagsList.Add(_transmitT7);
                _sensorFlagsList.Add(_transmitP7);
                _sensorFlagsList.Add(_transmitO1);
                _sensorFlagsList.Add(_transmitO2);
                _sensorFlagsList.Add(_transmitP8);
                _sensorFlagsList.Add(_transmitT8);
                _sensorFlagsList.Add(_transmitFC6);
                _sensorFlagsList.Add(_transmitF4);
                _sensorFlagsList.Add(_transmitF8);
                _sensorFlagsList.Add(_transmitAF4);

                // Raise the transmitting flag
                _isTransmittingData = true;
                
                // Save the transmission start time
                _transmissionStartTime = DateTime.Now;

                // Start the function to keep the time updated
                _statisticsUpdaterThread = new(UpdateUIStatics);
                _statisticsUpdaterThread.Start();

                // Change button to stop button
                TransmissionButton.BackgroundColor = _red;
                TransmissionButton.Text = "Stop";

                // Enable the record button
                RecordButton.IsEnabled = true;
            }
        }

        public void OnRecordClicked(Object sender, EventArgs e)
        {
            Debug.Write("Record Clicked...");

            // Check if we are not recording
            if(_isRecording)
            {
                Debug.WriteLine("\tStopping Recording");

                // Lower the recording flag
                _isRecording = false;

                // Zero the recordings saved variable
                _statsVariableMutex.WaitOne();
                _numSamplingsSaved = 0;
                _statsVariableMutex.ReleaseMutex();

                // Close the file
                _fs.Dispose();

                // Enable save options
                ToggleSaveOptions();

                // Update the recordings text
                _statsVariableMutex.WaitOne();
                RecordingsSaved.Text = _numRecordings.ToString();
                _statsVariableMutex.ReleaseMutex();

                // Set the button to green
                RecordButton.BackgroundColor = Colors.Green;

                // Update button text
                RecordButton.Text = "Record";

                // Enable the transmission button
                TransmissionButton.IsEnabled = true;
            }
            else
            {
                Debug.WriteLine("\tStarting Recording.");

                // Increase the number of recordings
                _statsVariableMutex.WaitOne();
                _numRecordings++;
                _statsVariableMutex.ReleaseMutex();

                // Grab the volunteers name
                _volunteerName = VolunteerName.Text;

                // Grab the session ID
                _sessionID = SessionIdentifier.Text;

                // Get the filename
                string filename = "";

                // Check if we have a volunteer name
                if (_volunteerName is not null)
                {
                    filename += _volunteerName.ToString() + ' ';
                }

                // Check if we have a session Id
                if (_sessionID is not null)
                {
                    filename += '{' + _sessionID.ToString() + "} ";
                }

                // Add remaining filename items
                _statsVariableMutex.WaitOne();
                filename += $"[{DateTime.Now.ToString("MM-dd-yyyy HHmm")}] ({_numRecordings}).csv";
                _statsVariableMutex.ReleaseMutex();

                // Open the file stream
                Debug.WriteLine($"\tCreating File: {filename}");
                _fs = new FileStream(Path.Combine(_saveLocation, filename), FileMode.Append, FileAccess.Write);

                // Save the headers to the file
                WriteDataToFile(_fileHeader);

                // Disable the transmission button
                TransmissionButton.IsEnabled = false;

                // Raise the recording flag
                _isRecording = true;

                // Save the recording start time
                _recordingStartTime = DateTime.Now;

                // Disable save options
                ToggleSaveOptions();

                // Set the button to red
                RecordButton.BackgroundColor = Colors.Red;

                // Update button text
                RecordButton.Text = "Stop";
            }
        }

        private void DisplayHeadsetStatus()
        {
            // Check if the headset is connected
            if(_isHeadsetConnected)
            {
                HeadsetConnectionStatus.Text = "Connected";
                HeadsetConnectionStatus.TextColor = (Color)Application.Current.Resources["Good"];
            }
            else
            {
                HeadsetConnectionStatus.Text = "Disconnected";
                HeadsetConnectionStatus.TextColor = (Color)Application.Current.Resources["Bad"];
            }
        }

        private void ToggleSaveOptions()
        {
            VolunteerName.IsEnabled = !VolunteerName.IsEnabled;
            SessionIdentifier.IsEnabled= !SessionIdentifier.IsEnabled;
            SelectLocationButton.IsEnabled= !SelectLocationButton.IsEnabled;
        }

        private void DisplayPath(string path)
        {
            // Check if the path is longer than max length
            if (path.Length > _maxPathLength)
            {
                // Make the shortened path
                string tempPath = "...";
                bool foundSlash = false;

                // Iterate the path starting _maxPathLength - 3 from the end
                for(int i = (path.Length - (_maxPathLength - 3)); i < path.Length; i++)
                {
                    // Check if the current character is a valid starting point
                    if (!foundSlash && (path[i] is '/' || path[i] is '\\'))
                    {
                        foundSlash = true;
                    }
                    // If we have not found a starting point increment
                    else if(!foundSlash)
                    {
                        continue;
                    }

                    // Add character to tempPath
                    tempPath += path[i];
                }

                // Set the text
                SaveLocationPath.Text = tempPath;
            }
            else
            {
                // Set the text
                SaveLocationPath.Text = path;
            }
        }

        private void SubscribedOK(object sender, Dictionary<string, JArray> e)
        {
            foreach (string key in e.Keys)
            {
                if (key is "pow")
                {
                    // print header
                    ArrayList? header = e[key].ToObject<ArrayList>();

                    // Check if header is null
                    if (header is null)
                    {
                        return;
                    }
                    
                    //add timeStamp to header
                    header.Insert(0, "Timestamp");

                    // Save as the file header
                    _fileHeader = header;
                }
            }
        }

        private void OnBandPowerOK(object sender, ArrayList eegData)
        {
            // Check transmitting flag
            if(_isTransmittingData)
            {   
                // Send data over OSC
                WriteDataToOSC(eegData);
            }

            // Check recording flag
            if(_isRecording)
            {
                // Save data to CSV
                WriteDataToFile(eegData);
            }
        }

        private void ToggleWaveCheckboxs()
        {
            // Toggle checkboxs
            ThetaCheckBox.IsEnabled = !ThetaCheckBox.IsEnabled;
            AlphaCheckBox.IsEnabled = !AlphaCheckBox.IsEnabled;
            BetaLCheckBox.IsEnabled = !BetaLCheckBox.IsEnabled;
            BetaHCheckBox.IsEnabled = !BetaHCheckBox.IsEnabled;
            GammaCheckBox.IsEnabled = !GammaCheckBox.IsEnabled;
        }

        private void ToggleSensorCheckboxs()
        {
            // Toggle checkboxs
            AF3CheckBox.IsEnabled = !AF3CheckBox.IsEnabled;
            F7CheckBox.IsEnabled = !F7CheckBox.IsEnabled;
            F3CheckBox.IsEnabled = !F3CheckBox.IsEnabled;
            FC5CheckBox.IsEnabled = !FC5CheckBox.IsEnabled;
            T7CheckBox.IsEnabled = !T7CheckBox.IsEnabled;
            P7CheckBox.IsEnabled = !P7CheckBox.IsEnabled;
            O1CheckBox.IsEnabled = !O1CheckBox.IsEnabled;
            O2CheckBox.IsEnabled = !O2CheckBox.IsEnabled;
            P8CheckBox.IsEnabled = !P8CheckBox.IsEnabled;
            T8CheckBox.IsEnabled = !T8CheckBox.IsEnabled;
            FC6CheckBox.IsEnabled = !FC6CheckBox.IsEnabled;
            F4CheckBox.IsEnabled = !F4CheckBox.IsEnabled;
            F8CheckBox.IsEnabled = !F8CheckBox.IsEnabled;
            AF4CheckBox.IsEnabled = !AF4CheckBox.IsEnabled;
        }

        private void ToggleOSCEntries()
        {
            // Toggle entries
            OSCIpAddress.IsEnabled = !OSCIpAddress.IsEnabled;
            OSCPortNum.IsEnabled = !OSCPortNum.IsEnabled;
        }

        private void WriteDataToOSC(ArrayList data)
        {
            // Check if the OSC instance is valid
            if (_osc is null)
            {
                return;
            }

            // Check for a valid array of data
            if(data.Count is 0)
            {
                return;
            }

            // Claim the transmission mutex
            _transmissionMutex.WaitOne();

            // Output data in OSC protocol
            // ... /{frequency}/{sensor} {value}
            for(int i = 1; i < data.Count; i++)
            {
                // Determine the band
                string band = _bands[(i - 1) % 5];

                // Determine the sensor
                string sensor = _sensors[(i - 1) / 5];

                // Check if that band and sensor is being transmitted
                if (_bandFlagsList[(i - 1) % 5] && _sensorFlagsList[(i - 1) / 5])
                {
                    Debug.WriteLine($"Sending OSC Message: /{band}/{sensor} {Convert.ToString(data[i])}");

                    // Construct the arguments
                    object[] args = { Convert.ToSingle(data[i]) };

                    // Send the OSC message
                    _osc.SendMessage($"/{band}/{sensor}", args);

                    // Update the samples sent variable
                    _statsVariableMutex.WaitOne();
                    _numSamplingsTransmitted++;
                    Debug.WriteLine($"Sampling Transmitted: {_numSamplingsTransmitted}");
                    _statsVariableMutex.ReleaseMutex();
                }
            }

            // Release the transmission mutex
            _transmissionMutex.ReleaseMutex();
        }

        private void WriteDataToFile(ArrayList data)
        {

            // Check if the OSC instance is valid
            if (_osc is null)
            {
                return;
            }

            // Check for a valid array of data
            if (data is null)
            {
                return;
            }
            else if (data.Count is 0)
            {
                return;
            }

            // Save the data to the file
            int i = 0;
            for(; i < data.Count - 1; i++) {
                byte[] val = Encoding.UTF8.GetBytes(data[i].ToString() + ", ");

                if (_fs is not null)
                    _fs.Write(val, 0, val.Length);
                else
                    break;
            }

            // Save the last element with a newline character
            byte[] lastVal = Encoding.UTF8.GetBytes(data[i].ToString() + "\n");
            if (_fs is not null)
            {
                _fs.Write(lastVal, 0, lastVal.Length);
            }

            // Update the sampling recorded variable
            _statsVariableMutex.WaitOne();
            _numSamplingsSaved += Convert.ToUInt16(data.Count - 1);
            _statsVariableMutex.ReleaseMutex();
        }

        private void ZeroOSCChannels()
        {
            // Check that there is a valid OSC instance
            if(_osc is null)
            {
                return;
            }

            // Claim the transmission mutex
            _transmissionMutex.WaitOne();

            // Ensure that transmission has been cancelled
            if (!_isTransmittingData)
            {
                // Iterate through the sensors
                for (int i = 0; i < _sensors.Length; i++)
                {
                    // Check if the sensor is in use
                    if (_sensorFlagsList[i])
                    {
                        // Iterate through the bands
                        for (int j = 0; j < _bands.Length; j++)
                        {
                            // Check if the band is in use
                            if (_bandFlagsList[j])
                            {
                                // Zero out the channel
                                object[] args = { 0.0f };
                                _osc.SendMessage($"/{_bands[j]}/{_sensors[i]}", args);
                            }
                        }
                    }
                }
            }

            // Release the transmission mutex
            _transmissionMutex.ReleaseMutex();
        }

        private void UpdateUIStatics()
        {
            // Loop while transmitting data
            while (_isTransmittingData)
            {
                UpdateTransmissionStats();

                // Check if we are recording data
                if (_isRecording)
                {
                    UpdateRecordingStats();
                }

                // Delay to prevent excessive CPU usuage
                Thread.Sleep(1000);
            }
        }

        private void UpdateTransmissionStats()
        {
            try
            {
                // Calculate the elapsed time
                var elapsedTime = DateTime.Now - _transmissionStartTime;

                // Update the UI on the main thread
                MainThread.BeginInvokeOnMainThread(() => 
                {
                    _statsVariableMutex.WaitOne();
                    ElapsedTransmissionTime.Text = elapsedTime.ToString(@"hh\:mm\:ss");
                    TotalSamplingsTransmitted.Text = _numSamplingsTransmitted.ToString();
                    _statsVariableMutex.ReleaseMutex();
                });
            }
            catch
            {
                // PASS
            }
        }

        private void UpdateRecordingStats()
        {
            try
            {
                // Calculat ethe elapsed time
                var elapsedTime = DateTime.Now - _recordingStartTime;

                // Update the UI on the main thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ElapsedRecordTime.Text = elapsedTime.ToString(@"hh\:mm\:ss");
                    _statsVariableMutex.WaitOne();
                    TotalSamplingsSaved.Text = _numSamplingsSaved.ToString();
                    _statsVariableMutex.ReleaseMutex();
                });
            }
            catch
            {
                // PASS
            }
        }
    }
}