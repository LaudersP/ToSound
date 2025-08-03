using System;
using System.Diagnostics;
using System.Threading;

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

        private readonly Mutex _dataPointsTransmittedMutex = new();
        private uint _dataPointsTransmitted = 0;

        public enum TransmissionStates
        {
            OFF,
            STARTING,
            SELECT,
            ALL,
            STOPPING
        };

        public enum PlaybackStates
        {
            ALL,
            BASELINE,
            TRANSITION_TO_TH,
            TRANSIENT_HYPOFRONTALITY,
            TRANSITION_TO_FLOW,
            FLOW
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



        // #################################################
        // ##                                             ##
        // ##    All Code Above This Point is Approved    ##
        // ##                                             ##
        // #################################################





        // Method for setting the OSC information
        public void SetOSCInfo(string ipAddress, int portNum)
        {
            // Initialize the OSC client
            _oscClient = new OSC(ipAddress, portNum);

            // Reset the total samplings transmitted
            _dataPointsTransmitted = 0;
        }

        // Method to send the playback state data via OSC
        public void PlayState(PlaybackStates playbackState, List<bool> enabledBands, List<bool> enabledSensors, CancellationToken cancellationToken)
        {
            // Send the desired states data
            switch (playbackState)
            {
                case PlaybackStates.BASELINE:
                    SendDataByIndexes(_baselineDataIndexes, enabledBands, enabledSensors, cancellationToken);
                    break;
                case PlaybackStates.TRANSITION_TO_TH:
                    SendDataByIndexes(_transitionToThDataIndexes, enabledBands, enabledSensors, cancellationToken);
                    break;
                case PlaybackStates.TRANSIENT_HYPOFRONTALITY:
                    SendDataByIndexes(_thDataIndexes, enabledBands, enabledSensors, cancellationToken);
                    break;
                case PlaybackStates.TRANSITION_TO_FLOW:
                    SendDataByIndexes(_transitionToFlowDataIndexes, enabledBands, enabledSensors, cancellationToken);
                    break;
                case PlaybackStates.FLOW:
                    SendDataByIndexes(_flowDataIndexes, enabledBands, enabledSensors, cancellationToken);
                    break;
            }
        }

        // Method for sending data based on index positions
        public void SendDataByIndexes(List<int> indexes, List<bool> enabledBands, List<bool> enabledSensors, CancellationToken cancellationToken)
        {
            // Check if there is a initialized OSC client
            if (_oscClient == null)
            {
                throw new Exception("OSC client is not initialized.");
            }

            // Iterate through the indexes
            DateTime lastTransmitted = DateTime.Now;
            foreach (int index in indexes)
            {
                // Update the current index
                _currentIndex = index;

                // Get a line of data from the file
                string line = GetDataLineAtIndex(_currentIndex);

                // Break up the line of data
                string[] data = BreakupDataRow(line);

                // Delay until the transmission time has passed
                while ((DateTime.Now - lastTransmitted).TotalMilliseconds <= _dataTransmissionDelay) { }

                // Update the last transmitted time
                lastTransmitted = DateTime.Now;

                // Iterate through the data
                bool inUse;
                for (int i = 1; i <= (data.Length - 2); i++)
                {
                    inUse = true;

                    // Get the current sensor index
                    int sensorIndex = (i - 1) / _bands.Length;

                    // Check if this sensor is selected
                    if (!enabledSensors[sensorIndex])
                    {
                        inUse = false;
                    }

                    // Get the current band index
                    int bandIndex = (i - 1) % _bands.Length;

                    // Check if this band is selected
                    if (!enabledBands[bandIndex])
                    {
                        inUse = false;
                    }

                    // Construct the OSC address
                    string oscAddress = $"/{_bands[bandIndex]}/{_sensors[sensorIndex]}";

                    // Construct the OSC message, checking if this sensor and band are in use
                    object[] oscMessage;
                    if (inUse)
                    {
                        // Send the data as a float
                        oscMessage = [Convert.ToSingle(data[i])];
                        Debug.WriteLine($"[STATE] - Sending: {data[i]}");

                        // Increase the number of data points transmitted
                        _dataPointsTransmittedMutex.WaitOne();
                        _dataPointsTransmitted++;
                        _dataPointsTransmittedMutex.ReleaseMutex();
                    }
                    else
                    {
                        // Send a zero value if not in use
                        oscMessage = [0.0f];
                    }

                    // Send the OSC message
                    _oscClient.SendMessage(oscAddress, oscMessage);

                    try
                    {
                        // Check for cancellation
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        }

        // Method for getting a single line of data at a specific index
        private string GetDataLineAtIndex(int index)
        {
            // Check if the file path is set
            if (_filePath == null)
            {
                throw new Exception("File path is not set.");
            }

            // Check if the index is in range of the file
            if (index < 0 || index >= _fileLength)
            {
                throw new Exception($"Index is out of range (0 - {_fileLength}).");
            }

            return File.ReadLines(_filePath).ElementAt(index);
        }

        // Method for breaking down a data row into a data list
        private string[] BreakupDataRow(string line)
        {
            return line.Replace(", ", ",").Split(",");
        }

        // Method for stopping transmission
        public void StopTransmission()
        {
            // Iteracte through each sensor
            foreach (string sensor in _sensors)
            {
                // Iterate through each band
                foreach (string band in _bands)
                {
                    // Construct the OSC address
                    string oscAddress = $"/{band}/{sensor}";

                    // Send a zero message
                    _oscClient?.SendMessage(oscAddress, new object[] { 0.0f });
                }
            }

            // Close the OSC connection
            _oscClient?.Close();
        }

        // Method for zeroing out the OSC client
        private void ZeroOSCClient()
        {
            
        }

        // Method for getting the number of data points sent
        public string GetTotalSamplings()
        {
            // Get a local copy of the total data points sent
            _dataPointsTransmittedMutex.WaitOne();
            uint totalSamplings = _dataPointsTransmitted;
            _dataPointsTransmittedMutex.ReleaseMutex();

            return totalSamplings.ToString();
        }

        // Method for sending all data
        public void PlayFile(List<bool> enabledBands, List<bool> enabledSensors, CancellationToken cancellationToken)
        {
            // Check if there is a initialized OSC client
            if (_oscClient == null)
            {
                throw new Exception("OSC client is not initialized.");
            }

            // Iterate through the indexes
            _currentIndex = 1; // Skips ths header row
            DateTime lastTransmitted = DateTime.Now;
            while (_currentIndex < _fileLength)
            {
                // Get a line of data from the file
                string line = GetDataLineAtIndex(_currentIndex++);

                // Break up the line of data
                string[] data = BreakupDataRow(line);

                // Delay until the transmission time has passed
                while ((DateTime.Now - lastTransmitted).TotalMilliseconds <= _dataTransmissionDelay) { }

                // Update the last transmitted time
                lastTransmitted = DateTime.Now;

                // Iterate through the data
                bool inUse;
                for (int i = 1; i <= (data.Length - 2); i++)
                {
                    inUse = true;

                    // Get the current sensor index
                    int sensorIndex = (i - 1) / _bands.Length;

                    // Check if this sensor is selected
                    if (!enabledSensors[sensorIndex])
                    {
                        inUse = false;
                    }

                    // Get the current band index
                    int bandIndex = (i - 1) % _bands.Length;

                    // Check if this band is selected
                    if (!enabledBands[bandIndex])
                    {
                        inUse = false;
                    }

                    // Construct the OSC address
                    string oscAddress = $"/{_bands[bandIndex]}/{_sensors[sensorIndex]}";

                    // Construct the OSC message, checking if this sensor and band are in use
                    object[] oscMessage;
                    if (inUse)
                    {
                        // Send the data as a float
                        oscMessage = [Convert.ToSingle(data[i])];
                        Debug.WriteLine($"[FILL] - Sending: {data[i]}");

                        // Increase the number of data points transmitted
                        _dataPointsTransmittedMutex.WaitOne();
                        _dataPointsTransmitted++;
                        _dataPointsTransmittedMutex.ReleaseMutex();
                    }
                    else
                    {
                        // Send a zero value if not in use
                        oscMessage = [0.0f];
                    }

                    // Send the OSC message
                    _oscClient.SendMessage(oscAddress, oscMessage);

                    try
                    {
                        // Check for cancellation
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        }
    }
}
