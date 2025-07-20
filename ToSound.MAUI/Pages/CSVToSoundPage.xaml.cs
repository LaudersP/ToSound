using CortexAccess;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Windows.Devices.Display.Core;
using Windows.Media.AppBroadcasting;
using Windows.Networking.NetworkOperators;
using ToSound.Core;

namespace ToSound.Pages;

public partial class CSVToSoundPage : ContentPage
{
    // Flags
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

    // Logic Variables
    private string _selectedFilePath = string.Empty;
    private MindToSoundEmulator? _emulator = null;
    private const int _maxFilePathLength = 60;
    private int _lengthOfFile = 0;
    private bool[] _availablePlaybackStates = [];
    private bool _statesEnabled = false;

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
            ToggleWaveCheckboxes();

            // Disable the sensor checkboxes
            ToggleSensorCheckboxes();

            // Disable the playback state buttons
            TogglePlaybackStates();
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
            return;
        }
        
        // Enable the send button
        TransmissionButton.IsEnabled = true;

        // Get the available playback states
        _availablePlaybackStates = _emulator.GetAvailablePlaybackStates();

        // Enable the playback state buttons
        ToggleSelectPlaybackStates(_availablePlaybackStates);

        // Enable the wave frequency checkboxes
        ToggleWaveCheckboxes();

        // Enable the sensor checkboxes
        ToggleSensorCheckboxes();
    }

    private void TogglePlaybackStates()
    {
        // Toggle all states based on the current state
        ToggleSelectPlaybackStates([true, true, true, true, true]);
    }

    private void ToggleSelectPlaybackStates(bool[] selectedPlaybackStates)
    {
        // Toggle the states enabled flag
        _statesEnabled = !_statesEnabled;

        // Enable the loop switch
        LoopPlaybackToggle.IsEnabled = _statesEnabled;

        BaselineBtn.IsEnabled = selectedPlaybackStates[0] && _statesEnabled;
        TransitionToThBtn.IsEnabled = selectedPlaybackStates[1] && _statesEnabled;
        TransientHypofrontalityBtn.IsEnabled = selectedPlaybackStates[2] && _statesEnabled;
        TransitionToFlowBtn.IsEnabled = selectedPlaybackStates[3] && _statesEnabled;
        FlowBtn.IsEnabled = selectedPlaybackStates[4] && _statesEnabled;
    }

    private void ToggleWaveCheckboxes()
    {
        ThetaCheckBox.IsEnabled = !ThetaCheckBox.IsEnabled;
        AlphaCheckBox.IsEnabled = !AlphaCheckBox.IsEnabled;
        BetaLCheckBox.IsEnabled = !BetaLCheckBox.IsEnabled;
        BetaHCheckBox.IsEnabled = !BetaHCheckBox.IsEnabled;
        GammaCheckBox.IsEnabled = !GammaCheckBox.IsEnabled;
    }

    private void ToggleSensorCheckboxes()
    {
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


    // #################################################
    // ##                                             ##
    // ##    All Code Above This Point is Approved    ##
    // ##                                             ##
    // #################################################



    public void OnBaselineBtnClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Baseline Button Clicked");
    }

    public void OnTransitionToThBtnClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Transition to Transient Hypofrontality Button Clicked");
    }

    public void OnTransientHypofrontalityBtnClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Transient Hypofrontality Button Clicked");
    }

    public async void OnTransitionToFlowBtnClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Transition to Flow Button Clicked");
    }

    public async void OnFlowBtnClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Flow Button Clicked");
    }

    public async void OnTransmissionClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Transmission Button Clicked");
    }
}