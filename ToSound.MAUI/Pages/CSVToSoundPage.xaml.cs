using CortexAccess;
using Microsoft.UI.Composition;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ToSound.Core;

namespace ToSound.Pages;

public partial class CSVToSoundPage : ContentPage
{
    // Flags
    private bool _transmitTheta = false;
    private bool _transmitAlpha = false;
    private bool _transmitBetaL = false;
    private bool _transmitBetaH = false;
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

    // Logic Variables
    private string _selectedFilePath = string.Empty;
    private MindToSoundEmulator? _emulator = null;
    private const int _maxFilePathLength = 60;
    private int _lengthOfFile = 0;
    private bool[] _availablePlaybackStates = [];
    private bool _statesEnabled = false;
    private string _oscIP = "127.0.0.1";
    private int _oscPort = 55555;
    private MindToSoundEmulator.TransmissionStates _currentTransmissionState = MindToSoundEmulator.TransmissionStates.OFF;
    private MindToSoundEmulator.PlaybackStates _currentPlaybackState = MindToSoundEmulator.PlaybackStates.ALL;
    private readonly Mutex _transmissionLockout = new();
    private Thread? _transmissionThread = null;
    private CancellationTokenSource _transmissionCT = new();

    // UI Variables
    private Color _green = Color.FromArgb("#00AB66");
    private Color _red = Color.FromArgb("#CF142B");
    private DateTime _transmissionStartTime;


    public CSVToSoundPage()
    {
        InitializeComponent();

        // Set the file info text to empty
        FileLocationPath.Text = "";
        VolunteerName.Text = "";
        RecordingNum.Text = "";
        SessionId.Text = "";
        DataLength.Text = "";
        RecordingDate.Text = "";
        RecordingLength.Text = "";

        // Disable the playback state buttons
        BaselineBtn.IsEnabled = false;
        TransitionToThBtn.IsEnabled = false;
        TransientHypofrontalityBtn.IsEnabled = false;
        TransitionToFlowBtn.IsEnabled = false;
        FlowBtn.IsEnabled = false;

        // Disable the wave frequency checkboxes
        LoopPlaybackToggle.IsEnabled = false;
        ThetaCheckBox.IsEnabled = false;
        AlphaCheckBox.IsEnabled = false;
        BetaLCheckBox.IsEnabled = false;
        BetaHCheckBox.IsEnabled = false;
        GammaCheckBox.IsEnabled = false;

        // Disable the sensor checkboxes
        AF3CheckBox.IsEnabled = false;
        F7CheckBox.IsEnabled = false;
        F3CheckBox.IsEnabled = false;
        FC5CheckBox.IsEnabled = false;
        T7CheckBox.IsEnabled = false;
        P7CheckBox.IsEnabled = false;
        O1CheckBox.IsEnabled = false;
        O2CheckBox.IsEnabled = false;
        P8CheckBox.IsEnabled = false;
        T8CheckBox.IsEnabled = false;
        FC6CheckBox.IsEnabled = false;
        F4CheckBox.IsEnabled = false;
        F8CheckBox.IsEnabled = false;
        AF4CheckBox.IsEnabled = false;
    }

    private async void OnSelectFileButtonClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Select File Button Clicked...");

        // Check if a file is already loaded to disable the UI elements
        if(_selectedFilePath is not "")
        {
            // Disable the GUI Elements until file is validatedd
            TransmissionButton.IsEnabled = false;

            // Disable the wave frequency checkboxes
            WaveCheckboxesEnabled(false);

            // Disable the sensor checkboxes
            SensorCheckboxesEnabled(false);

            // Disable the playback state buttons
            PlaybackStatesEnabled(false);
        }

        // Create the supported file options
        var options = new PickOptions
        {
            PickerTitle = "Select CSV File",
            FileTypes = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    {DevicePlatform.WinUI, new[] {".csv"} },
                    {DevicePlatform.MacCatalyst, new[] {"csv", "public.comma-separated-values-text"} },
                    {DevicePlatform.macOS, new[] {"csv", "public.comma-separated-values-text"} }
                })
        };

        // Open the file explorer while limiting to the file options set above
        var result = await FilePicker.PickAsync(options);

        // Check if a file was selected
        if (result is null) return;
        
        // Get the file path
        _selectedFilePath = result.FullPath;

        // Attempt to create an construct emulator
        try
        {
            // Check if an emulator already exists
            if (_emulator is null)
            {
                // Create a new instance of the emulator
                _emulator = new MindToSoundEmulator(_selectedFilePath);
            }
            else
            {
                // Update the existing emulator with the new file path
                _emulator.SetFile(_selectedFilePath);
            }
        }
        // File reading/writing error
        catch (IOException ex)
        {
            await DisplayAlert("Error", $"{ex.Message}", "OK");
            return;
        }
        // Misc.
        catch (Exception ex)
        { 
            await DisplayAlert("Error", $"{ex.Message}", "OK");
            return;
        }

        // Display the file path
        DisplayPath(_selectedFilePath);

        // Check if the file contains valid formatting
        string fileName = Path.GetFileName(_selectedFilePath);
        if(fileName.Contains('{') && fileName.Contains('}') && 
           fileName.Contains('[') && fileName.Contains(']') && 
           fileName.Contains('(') && fileName.Contains(')'))
        {
            // Get and display the file info
            string volunteerName = fileName.Split("{")[0].Trim();
            VolunteerName.Text = volunteerName;

            string sessionId = fileName.Split("{")[1].Split("}")[0];
            SessionId.Text = sessionId;

            string recordingDate = fileName.Split("[")[1].Split("]")[0];
            RecordingDate.Text = recordingDate;

            string recordingNum = fileName.Split("(")[1].Split(")")[0];
            RecordingNum.Text = recordingNum;

            _lengthOfFile = _emulator.GetFileLength();
            DataLength.Text = (_lengthOfFile - 1).ToString();

            double lengthSeconds = Double.Round(_emulator.GetTransmissionDelay() * (_lengthOfFile - 1) / 1000, 0);
            byte lengthMinutes = 0;
            while (lengthSeconds > 60)
            {
                lengthSeconds -= 60;
                lengthMinutes++;
            }
            byte lengthHours = 0;
            while (lengthMinutes > 60)
            {
                lengthMinutes -= 60;
                lengthHours++;
            }
            RecordingLength.Text = $"{lengthHours}:{lengthMinutes}:{Convert.ToByte(Double.Round(lengthSeconds, 2))}";
        }
        else
        {
            await DisplayAlert("Warning", "Unable to display file related information.", "CONTINUE");
        }
        
        // Enable the send button
        TransmissionButton.IsEnabled = true;

        // Get the available playback states
        _availablePlaybackStates = _emulator.GetAvailablePlaybackStates();

        // Enable the playback state buttons
        SelectPlaybackStatesEnabled(_availablePlaybackStates, true);

        // Enable the wave frequency checkboxes
        WaveCheckboxesEnabled(true);

        // Enable the sensor checkboxes
        SensorCheckboxesEnabled(true);
    }

    private void PlaybackStatesEnabled(bool isEnabled)
    {
        // Toggle all states based on the current state
        SelectPlaybackStatesEnabled([true, true, true, true, true], isEnabled);
    }

    private void SelectPlaybackStatesEnabled(bool[] selectedPlaybackStates, bool isEnabled)
    {
        // Enable the loop switch
        LoopPlaybackToggle.IsEnabled = isEnabled;

        BaselineBtn.IsEnabled = selectedPlaybackStates[0] && isEnabled;
        TransitionToThBtn.IsEnabled = selectedPlaybackStates[1] && isEnabled;
        TransientHypofrontalityBtn.IsEnabled = selectedPlaybackStates[2] && isEnabled;
        TransitionToFlowBtn.IsEnabled = selectedPlaybackStates[3] && isEnabled;
        FlowBtn.IsEnabled = selectedPlaybackStates[4] && isEnabled;
    }

    private void WaveCheckboxesEnabled(bool isEnabled)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoopPlaybackToggle.IsEnabled = isEnabled;
            ThetaCheckBox.IsEnabled = isEnabled;
            AlphaCheckBox.IsEnabled = isEnabled;
            BetaLCheckBox.IsEnabled = isEnabled;
            BetaHCheckBox.IsEnabled = isEnabled;
            GammaCheckBox.IsEnabled = isEnabled;

            // Check if the check boxes need to be rechecked (prevents the grayed out)
            if (isEnabled)
            {
                for(int i = 0; i < 2; i++)
                {
                    LoopPlaybackToggle.IsToggled = !LoopPlaybackToggle.IsToggled;
                    ThetaCheckBox.IsChecked = !ThetaCheckBox.IsChecked;
                    AlphaCheckBox.IsChecked = !AlphaCheckBox.IsChecked;
                    BetaLCheckBox.IsChecked = !BetaLCheckBox.IsChecked;
                    BetaHCheckBox.IsChecked = !BetaHCheckBox.IsChecked;
                    GammaCheckBox.IsChecked = !GammaCheckBox.IsChecked;
                    Thread.Sleep(100);
                }
            }
        });
    }

    private void SensorCheckboxesEnabled(bool isEnabled)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            AF3CheckBox.IsEnabled = isEnabled;
            F7CheckBox.IsEnabled = isEnabled;
            F3CheckBox.IsEnabled = isEnabled;
            FC5CheckBox.IsEnabled = isEnabled;
            T7CheckBox.IsEnabled = isEnabled;
            P7CheckBox.IsEnabled = isEnabled;
            O1CheckBox.IsEnabled = isEnabled;
            O2CheckBox.IsEnabled = isEnabled;
            P8CheckBox.IsEnabled = isEnabled;
            T8CheckBox.IsEnabled = isEnabled;
            FC6CheckBox.IsEnabled = isEnabled;
            F4CheckBox.IsEnabled = isEnabled;
            F8CheckBox.IsEnabled = isEnabled;
            AF4CheckBox.IsEnabled = isEnabled;

            // Check if the check boxes need to be rechecked (prevents the grayed out)
            if (isEnabled)
            {
                for(int i = 0; i < 2; i++)
                {
                    AF3CheckBox.IsChecked = !AF3CheckBox.IsChecked;
                    F7CheckBox.IsChecked = !F7CheckBox.IsChecked;
                    F3CheckBox.IsChecked = !F3CheckBox.IsChecked;
                    FC5CheckBox.IsChecked = !FC5CheckBox.IsChecked;
                    T7CheckBox.IsChecked = !T7CheckBox.IsChecked;
                    P7CheckBox.IsChecked = !P7CheckBox.IsChecked;
                    O1CheckBox.IsChecked = !O1CheckBox.IsChecked;
                    O2CheckBox.IsChecked = !O2CheckBox.IsChecked;
                    P8CheckBox.IsChecked = !P8CheckBox.IsChecked;
                    T8CheckBox.IsChecked = !T8CheckBox.IsChecked;
                    FC6CheckBox.IsChecked = !FC6CheckBox.IsChecked;
                    F4CheckBox.IsChecked = !F4CheckBox.IsChecked;
                    F8CheckBox.IsChecked = !F8CheckBox.IsChecked;
                    AF4CheckBox.IsChecked = !AF4CheckBox.IsChecked;
                    Thread.Sleep(100);
                }
            }
        });
    }

    private void DisplayPath(string path)
    {
        // Check if the file path is longer than the max length
        if (path.Length <= _maxFilePathLength)
        {
            FileLocationPath.Text = path;
            return;
        }

        string tempPath = "...";
        bool foundSlash = false;

        // Iterate through the path starting `_maxFilePathLength - 3` characters from the end
        for (int i = path.Length - _maxFilePathLength + 3; i < path.Length; i++)
        {
            // Check if the current character is a valid starting point
            if (!foundSlash && (path[i] == '/' || path[i] == '\\'))
            {
                // If it is, set the fla
                foundSlash = true;
            }
            // If the current character is not a valid starting point
            else if (!foundSlash)
            {
                continue;
            }

            // Append the character to the temporary path
            tempPath += path[i];
        }

        // Set the file path text to the shortened version
        FileLocationPath.Text = tempPath;
    }

    public void OnThetaClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Theta Wave Frequencies Clicked");

        // Set the theta transmission flag
        _transmitTheta = ThetaCheckBox.IsChecked;
    }

    public void OnAlphaClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Alpha Wave Frequencies Clicked");

        // Set the alpha transmission flag
        _transmitAlpha = AlphaCheckBox.IsChecked;
    }

    public void OnBetaLClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Beta Low Wave Frequencies Clicked");

        // Set the beta low transmission flag
        _transmitBetaL = BetaLCheckBox.IsChecked;
    }

    public void OnBetaHClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Beta High Wave Frequencies Clicked");

        // Set the beta high transmission flag
        _transmitBetaH = BetaHCheckBox.IsChecked;
    }

    public void OnGammaClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Gamma Wave Frequencies Clicked");

        // Set the gamma transmission flag
        _transmitGamma = GammaCheckBox.IsChecked;
    }

    public void OnAF3Clicked(object sender, EventArgs e)
    {
        Debug.WriteLine("AF3 Clicked");

        // Set the AF3 transmission flag
        _transmitAF3 = AF3CheckBox.IsChecked;
    }

    public void OnF7Clicked(object sender, EventArgs e)
    {
        Debug.WriteLine("F7 Clicked");

        // Set the F7 transmission flag
        _transmitF7 = F7CheckBox.IsChecked;
    }

    public void OnF3Clicked(object sender, EventArgs e)
    {
        Debug.WriteLine("F3 Clicked");

        // Set the F3 transmission flag
        _transmitF3 = F3CheckBox.IsChecked;
    }

    public void OnFC5Clicked(object sender, EventArgs e)
    {
        Debug.WriteLine("FC5 Clicked");

        // Set the FC5 transmission flag
        _transmitFC5 = FC5CheckBox.IsChecked;
    }

    public void OnT7Clicked(object sender, EventArgs e)
    {
        Debug.WriteLine("T7 Clicked");

        // Set the T7 transmission flag
        _transmitT7 = T7CheckBox.IsChecked;
    }

    public void OnP7Clicked(object sender, EventArgs e)
    {
        Debug.WriteLine("P7 Clicked");

        // Set the P7 transmission flag
        _transmitP7 = P7CheckBox.IsChecked;
    }

    public void OnO1Clicked(object sender, EventArgs e)
    {
        Debug.WriteLine("O1 Clicked");

        // Set the O1 transmission flag
        _transmitO1 = O1CheckBox.IsChecked;
    }

    public void OnAF4Clicked(object sender, EventArgs e)
    {
        Debug.WriteLine("AF4 Clicked");

        // Set the AF4 transmission flag
        _transmitAF4 = AF4CheckBox.IsChecked;
    }

    public void OnF8Clicked(object sender, EventArgs e)
    {
        Debug.WriteLine("F8 Clicked");

        // Set the F8 transmission flag
        _transmitF8 = F8CheckBox.IsChecked;
    }

    public void OnF4Clicked(object sender, EventArgs e)
    {
        Debug.WriteLine("F4 Clicked");

        // Set the F4 transmission flag
        _transmitF4 = F4CheckBox.IsChecked;
    }

    public void OnFC6Clicked(object sender, EventArgs e)
    {
        Debug.WriteLine("FC6 Clicked");

        // Set the FC6 transmission flag
        _transmitFC6 = FC6CheckBox.IsChecked;
    }

    public void OnT8Clicked(object sender, EventArgs e)
    {
        Debug.WriteLine("T8 Clicked");

        // Set the T8 transmission flag
        _transmitT8 = T8CheckBox.IsChecked;
    }

    public void OnP8Clicked(object sender, EventArgs e)
    {
        Debug.WriteLine("P8 Clicked");

        // Set the P8 transmission flag
        _transmitP8 = P8CheckBox.IsChecked;
    }

    public void OnO2Clicked(object sender, EventArgs e)
    {
        Debug.WriteLine("O2 Clicked");

        // Set the O2 transmission flag
        _transmitO2 = O2CheckBox.IsChecked;
    }

    public void OnBaselineBtnClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Baseline Button Clicked");
        PlaybackStateCaller(MindToSoundEmulator.PlaybackStates.BASELINE);
    }

    public void OnTransitionToThBtnClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Transition to Transient Hypofrontality Button Clicked");
        PlaybackStateCaller(MindToSoundEmulator.PlaybackStates.TRANSITION_TO_TH);
    }

    public void OnTransientHypofrontalityBtnClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Transient Hypofrontality Button Clicked");
        PlaybackStateCaller(MindToSoundEmulator.PlaybackStates.TRANSIENT_HYPOFRONTALITY);
    }

    public void OnTransitionToFlowBtnClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Transition to Flow Button Clicked");
        PlaybackStateCaller(MindToSoundEmulator.PlaybackStates.TRANSITION_TO_FLOW);
    }

    public void OnFlowBtnClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Flow Button Clicked");
        PlaybackStateCaller(MindToSoundEmulator.PlaybackStates.FLOW);
    }

    private async void PlaybackStateCaller(MindToSoundEmulator.PlaybackStates state)
    {
        // Set the current transmission state to start transmission
        _currentTransmissionState = MindToSoundEmulator.TransmissionStates.STARTING;

        // Set the current playback state
        _currentPlaybackState = state;

        // Handle the transmission based on the current state
        await HandleTransmission();
    }

    private async Task HandleTransmission()
    {
        // Act on the current transmission state
        switch(_currentTransmissionState)
        {
            case MindToSoundEmulator.TransmissionStates.OFF:
                {
                    Debug.WriteLine("[STATE MACHING] - Acting on state: \'OFF\'");
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        // Enable all UI elements
                        FileElementsEnabled(true);
                        WaveCheckboxesEnabled(true);
                        SensorCheckboxesEnabled(true);
                        SelectPlaybackStatesEnabled(_availablePlaybackStates, true);
                        OSCElementsEnabled(true);

                        // Restore the transmission button to send
                        TransmissionButton.Text = "Send";
                        TransmissionButton.BackgroundColor = _green;
                    });

                    break;
                }
            case MindToSoundEmulator.TransmissionStates.STARTING:
                {
                    Debug.WriteLine("[STATE MACHING] - Acting on state: \'STARTING\'");
                    // Set up the UI for transmission
                    SetupTransmission();

                    // Set the transmission start time
                    _transmissionStartTime = DateTime.Now;

                    // Check if there is a set state
                    if (_currentPlaybackState is MindToSoundEmulator.PlaybackStates.ALL)
                    {
                        // Set the transmission state to all states
                        _currentTransmissionState = MindToSoundEmulator.TransmissionStates.ALL;
                    }
                    else
                    {
                        // Set the transmission state to transmitting states
                        _currentTransmissionState = MindToSoundEmulator.TransmissionStates.SELECT;
                    }

                    // Create a new cancellation token
                    _transmissionCT = new CancellationTokenSource();

                    // Recursively call this method to handle the transmitting
                    await HandleTransmission();
                    break;
                }
            case MindToSoundEmulator.TransmissionStates.SELECT:
                {
                    Debug.WriteLine("[STATE MACHING] - Acting on state: \'SELECT\'");
                    // Play the selected playback state
                    _transmissionThread = new Thread(() =>
                    {
                        try
                        {
                            do
                            {
                                // Play the selected playback state
                                _emulator?.PlayState(_currentPlaybackState,
                                                    [_transmitTheta, _transmitAlpha, _transmitBetaL, _transmitBetaH, _transmitGamma],
                                                    [_transmitAF3, _transmitF7, _transmitF3, _transmitFC5, _transmitT7, _transmitP7, _transmitO1, _transmitO2, _transmitP8, _transmitT8, _transmitFC6, _transmitF4, _transmitF8, _transmitAF4],
                                                    _transmissionCT.Token);

                                // Check for cancellation
                                _transmissionCT.Token.ThrowIfCancellationRequested();
                            }
                            while (LoopPlaybackToggle.IsToggled && !_transmissionCT.Token.IsCancellationRequested);

                            // Cancel thread using the cancellation token
                            _transmissionCT.Cancel();
                        }
                        catch (OperationCanceledException) { }
                    });

                    // Handle the main thread items while transmitting
                    await HandleTransmissionHelper();
                    break;
                }
            case MindToSoundEmulator.TransmissionStates.ALL:
                {
                    Debug.WriteLine("[STATE MACHING] - Acting on state: \'ALL\'");
                    // Play the selected playback state
                    _transmissionThread = new Thread(() =>
                    {
                        try
                        {
                            // Play the selected playback state
                            _emulator?.PlayFile([_transmitTheta, _transmitAlpha, _transmitBetaL, _transmitBetaH, _transmitGamma],
                                                [_transmitAF3, _transmitF7, _transmitF3, _transmitFC5, _transmitT7, _transmitP7, _transmitO1, _transmitO2, _transmitP8, _transmitT8, _transmitFC6, _transmitF4, _transmitF8, _transmitAF4],
                                                _transmissionCT.Token);

                            // Cancel thread using the cancellation token
                            _transmissionCT.Cancel();
                        }
                        catch (OperationCanceledException) { }
                    });

                    // Handle the main thread items while transmitting
                    await HandleTransmissionHelper();
                    break;
                }
            case MindToSoundEmulator.TransmissionStates.STOPPING:
                {
                    Debug.WriteLine("[STATE MACHING] - Acting on state: \'STOPPING\'");
                    // Lock out the transmission mutex
                    _transmissionLockout.WaitOne();

                    // Stop the emulator
                    _emulator?.StopTransmission();

                    // Release the transmission mutex
                    _transmissionLockout.ReleaseMutex();

                    // Set the thread to null
                    _transmissionThread = null;

                    // Set the current transmission state to off
                    _currentTransmissionState = MindToSoundEmulator.TransmissionStates.OFF;

                    // Recursively call this method to handle the off state
                    await HandleTransmission();
                    break;
                }
        }
    }

    private  async Task HandleTransmissionHelper()
    {
        // Start the thread
        _transmissionThread?.Start();

        // Update UI statistics (acts as a pause from continuing from this point)
        await UpdateUIStats();

        // Set the current state to now be stopping
        _currentTransmissionState = MindToSoundEmulator.TransmissionStates.STOPPING;

        // Release the transmission mutex
        _transmissionLockout.ReleaseMutex();

        // Recursively call this method to handle the stopping state
        await HandleTransmission();
    }

    private void FileElementsEnabled(bool isEnabled)
    {
        SelectFileButton.IsEnabled = isEnabled;
    }

    private void OSCElementsEnabled(bool isEnabled)
    {
        OSCIpAddress.IsEnabled = isEnabled;
        OSCPortNum.IsEnabled = isEnabled;
    }

    private void SetupTransmission()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Set the transmission button to stop
            TransmissionButton.Text = "Stop";
            TransmissionButton.BackgroundColor = _red;

            // Disable all UI elements
            FileElementsEnabled(false);
            WaveCheckboxesEnabled(false);
            SensorCheckboxesEnabled(false);
            PlaybackStatesEnabled(false);
            OSCElementsEnabled(false);
        });

        // Get the OSC IP
        if(OSCIpAddress.Text is not null)
        {
            // Update the IP
            _oscIP = OSCIpAddress.Text;
        }

        // Get the OSC Port Number
        if(OSCPortNum.Text is not null)
        {
            // Update the port number
            _oscPort = Convert.ToInt32(OSCPortNum.Text);
        }

        // Lock out the transmission mutex
        _transmissionLockout.WaitOne();

        // Set the OSC trasmission info
        _emulator?.SetOSCInfo(_oscIP, _oscPort);
    }

    private async Task UpdateUIStats()
    {
        while(!_transmissionCT.Token.IsCancellationRequested)
        {
            // Update the UI
            UpdateUIStatsHelper();

            await Task.Delay(250);
        }

        // Update the UI one last time
        UpdateUIStatsHelper();
    }

    private void UpdateUIStatsHelper()
    {
        // Calculate the elapsed time
        string elapsedTime = (DateTime.Now - _transmissionStartTime).ToString(@"hh\:mm\:ss");

        // Get the total samplings sent
        string totalSamplings = _emulator.GetTotalSamplings();

        // Update the UI 
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ElapsedTransmissionTime.Text = elapsedTime;
            TotalSamplingsTransmitted.Text = totalSamplings;
        });
    }

    public async void OnTransmissionClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Transmission Button Clicked");

        // Act on the current state
        switch (_currentTransmissionState)
        {
            case MindToSoundEmulator.TransmissionStates.OFF:
                Debug.WriteLine("[Transmission Button] - Starting Transmission");
                // Set the current playback state to all
                _currentPlaybackState = MindToSoundEmulator.PlaybackStates.ALL;

                // Set the current transmission state to starting
                _currentTransmissionState = MindToSoundEmulator.TransmissionStates.STARTING;

                // Call the handler method
                await HandleTransmission();
                break;

            case MindToSoundEmulator.TransmissionStates.SELECT:
            case MindToSoundEmulator.TransmissionStates.ALL:
                Debug.WriteLine("[Transmission Button] - Stopping Transmission");
                // Cancel thread using the cancellation token
                _transmissionCT.Cancel();

                // Set the current transmission state to stopping
                _currentTransmissionState = MindToSoundEmulator.TransmissionStates.STOPPING;

                // Call the handler method
                await HandleTransmission();
                break;
        }
    }
}