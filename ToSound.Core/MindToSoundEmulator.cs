using System.Diagnostics;

namespace ToSound.Core
{
    public class MindToSoundEmulator
    {
        private OSC? _oscClient;
        private List<int> _baselineDataIndexes = [];
        private List<int> _transitionToThDataIndexes = [];
        private List<int> _thDataIndexes = [];
        private List<int> _transitionToFlowDataIndexes = [];
        private List<int> _flowDataIndexes = [];
        private string? _filePath;
        private int _fileLength = 0;

        private readonly string[] _bands = ["theta", "alpha", "betaL", "betaH", "gamma"];
        private readonly string[] _sensors = ["AF3", "F7", "F3", "FC5", "T7", "P7", "O1", "O2", "P8", "T8", "FC6", "F4", "F8", "AF4"];
        private int _currentIndex = 0;
        private double _dataTransmissionDelay = 0.0;

        public enum State
        {
            Baseline,
            TransitionToTH,
            TransientHypofrontality,
            TransitionToFlow,
            Flow
        }

        // Initialize the emulator with no parameters
        public MindToSoundEmulator()
        {
            _filePath = null;
            _oscClient = null;
        }

        // Initialize the emulator with a filename
        public MindToSoundEmulator(string filename)
        {
            SetFile(filename);
            _oscClient = null;
        }

        // Initialize the emulator with all parameters
        public MindToSoundEmulator(string filename, string ipAddress, int portNum)
        {
            SetFile(filename);
            SetOSCInfo(ipAddress, portNum);
        }

        // Method for setting the file
        public void SetFile(string filename)
        {
            // Set the filename
            _filePath = filename;

            // Attempt to open the file
            try
            {
                // Open the file
                OpenFile(filename);
            }
            catch (IOException ioex)
            {
                Debug.WriteLine($"[EMULATOR] File I/O Error: {ioex.Message}");

                // Check if the contents of the exception message
                if (ioex.Message.Contains("being used by another process"))
                {
                    throw new IOException("The file is being used by another process, please close it and try again.");
                }
                else if(ioex.Message.Contains("Could not find file"))
                {
                    throw new IOException("Could not find the file, please ensure you have the correct file path.");
                }
                else
                {
                    throw;
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EMULATOR] Error: {ex.Message}");
                throw;
            }

            if (_fileLength < 3)
            {
                throw new Exception("Please select a file with 3 or more lines of data.");
            }
        }

        // Method for opening the file
        private void OpenFile(string filename)
        {
            // Reset file related variables
            _fileLength = 0;
            _baselineDataIndexes = [];
            _transitionToThDataIndexes = [];
            _thDataIndexes = [];
            _transitionToFlowDataIndexes = [];
            _flowDataIndexes = [];

            // Read the file for line count and initialize the index lists
            bool readHeader = false;
            bool hasStates = true;
            foreach (string line in File.ReadLines(filename))
            {
                Debug.WriteLine($"{_fileLength}: {line}");
                // Break up the line of data
                string[] data = BreakupDataRow(line);

                // Check if we have read the header
                if (!readHeader)
                {
                    // Parse the data to see if it contains states
                    if (data[data.Length - 1] is not "State")
                    {
                        hasStates = false;
                    }

                    // Increase the fileLength
                    _fileLength++;

                    readHeader = true;
                    continue;
                }

                // Check if the file has states
                if (hasStates)
                {
                    // Add the line index to the states index list
                    switch (data[data.Length - 1])
                    {
                        case "Baseline":
                            _baselineDataIndexes.Add(_fileLength);
                            break;

                        case "Transition_to_TH":
                            _transitionToThDataIndexes.Add(_fileLength);
                            break;

                        case "Transient_Hypofrontality":
                            _thDataIndexes.Add(_fileLength);
                            break;

                        case "Transition_to_Flow":
                            _transitionToFlowDataIndexes.Add(_fileLength);
                            break;

                        case "Flow":
                            _flowDataIndexes.Add(_fileLength);
                            break;

                        case "MANUAL_REVIEW":
                            throw new Exception("Manual review of some states required before using the playback states.");
                    }
                }

                // Increase the fileLength
                _fileLength++;
            }

            // Get the first timestamp
            string[] firstRowData = BreakupDataRow(GetDataLineAtIndex(1));
            double firstTimestamp = Convert.ToDouble(firstRowData[0]);

            // Get the last timestamp
            string[] lastRowData = BreakupDataRow(GetDataLineAtIndex(_fileLength - 1));
            double lastTimestamp = Convert.ToDouble(lastRowData[0]);

            // Calculate the average time between data points
            _dataTransmissionDelay = ((lastTimestamp - firstTimestamp) / (_fileLength - 1)) * 1000; // Convert to milliseconds

            // Ensure the delay is not 0
            if (_dataTransmissionDelay <= 0)
            {
                // Default to 100 milliseconds if calculated delay is zero or negative
                _dataTransmissionDelay = 100;
            }
        }

        // Method for getting the files length
        public int GetFileLength()
        {
            return _fileLength;
        }

        // Method for getting the transmission delay
        public double GetTransmissionDelay()
        {
            return _dataTransmissionDelay;
        }

        // Method to return which states are present
        public bool[] GetAvailablePlaybackStates()
        {
            return [
                (_baselineDataIndexes.Count > 0),
                (_transitionToThDataIndexes.Count > 0),
                (_thDataIndexes.Count > 0),
                (_transitionToFlowDataIndexes.Count > 0),
                (_flowDataIndexes.Count > 0)
                ];
        }
    }
}
