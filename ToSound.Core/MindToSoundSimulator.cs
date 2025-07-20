using CoreOSC;
using System.Diagnostics;
using System.Net;
using ToSound.Core;

namespace ToSound.Core
{
    internal class MindToSoundSimulator
    {
        private readonly string _csvFilePath;
        private string _ipAddress;
        private int _portNum;
        private int _index;
        private readonly int _rowCount;
        private readonly double _transmissionDelay;
        private OSC _oscTransmitter;
        private readonly string[] _bands = ["theta", "alpha", "betaL", "betaH", "gamma"];
        private readonly string[] _sensors = ["AF3", "F7", "F3", "FC5", "T7", "P7", "O1", "O2", "P8", "T8", "FC6", "F4", "F8", "AF4"];
        private bool _transmission;

        private List<int> _baselineDataIndexs = [];
        private List<int> _transitionToThDataIndexs = [];
        private List<int> _thDataIndexs = [];
        private List<int> _transitionToFlowDataIndexs = [];
        private List<int> _flowDataIndexs = [];
        private bool _statesInUse;

        public enum State
        {
            Baseline,
            TransitionToTh,
            Th,
            TransitionToFlow,
            Flow
        }

        // Constructor
        public MindToSoundSimulator(string csvFilePath, string ipAddress, int portNum)
        {
            _csvFilePath = csvFilePath;
            _ipAddress = ipAddress;
            _portNum = portNum;
            _index = 0;
            _rowCount = GetFileLength();
            _statesInUse = false;

            // Validate the length (3 rows due to calculating the transmission delay)
            // ... Row 1 <Headers>
            // ... Row 2 <Timestamp, data, ..., data
            if (_rowCount < 3)
            {
                throw new Exception("Error: Please select a file with 3+ rows.");
            }

            _transmissionDelay = CalculateTransmissionDelay();

            ScrapeStates();
        }

        // Getter for the CSV file row count
        public int GetRowCount()
        {
            return _rowCount;
        }

        // Getter for the transmission delay
        public double GetTransmissionDelay()
        {
            return _transmissionDelay;
        }

        // Getter for the CSV row index
        public int GetCSVRowIndex()
        {
            return _index;
        }

        // Method for getting the length of the CSV file
        private int GetFileLength()
        {
            int count = 0;

            foreach (string line in File.ReadLines(_csvFilePath))
            {
                count++;
            }

            return count;
        }

        // Method for getting the next single line of data
        private string GetNextCSVLine()
        {
            // Check if we are at the end of the file
            if (_index == _rowCount)
            {
                return "EOF";
            }

            return File.ReadLines(_csvFilePath).ElementAt(_index++);
        }

        // Method for getting specific a single line of data
        private string GetCSVLine(int index)
        {
            // Check for valid index
            if (index < 0 || index >= _rowCount)
                throw new Exception($"Error: Index out of range (0 - {_rowCount}");

            return File.ReadLines(_csvFilePath).ElementAt(index);
        }

        // Method for setting the OSC IP address
        public void SetOSCIP(string ipAddress)
        {
            _ipAddress = ipAddress;
        }

        //  Method for setting the OSC port number
        public void SetOSCPort(int portNum)
        {
            _portNum = portNum;
        }

        // Method for breaking down a data row into individual data items
        private static string[] BreakupDataRow(string line)
        {
            string tempLine = line.Replace(", ", ",");
            return tempLine.Split(",");
        }

        // Method for getting the delay between each transmission
        private double CalculateTransmissionDelay()
        {
            // Read the first data row timestamp
            string firstDataRow = GetCSVLine(1);
            string[] firstDataRowEntries = BreakupDataRow(firstDataRow);
            double firstTimestamp = Convert.ToDouble(firstDataRowEntries[0]);

            // Read the last data row timestamp
            string lastDataRow = GetCSVLine(_rowCount - 1);
            string[] lastDataRowEntries = BreakupDataRow(lastDataRow);
            double lastTimestamp = Convert.ToDouble(lastDataRowEntries[0]);

            // Calculate the average transmission delay
            return ((lastTimestamp - firstTimestamp) / (_rowCount - 1)) / (532f / 480f);
        }

        // Method for getting the OSC channel string
        private string GetOSCAddress(int index)
        {
            // Determine the band
            string band = _bands[(index - 1) % _bands.Length];

            // Assign the sensor
            string sensor = _sensors[(index - 1) / _bands.Length];

            // Construct the OSC address string
            return $"/{band}/{sensor}";
        }

        // Method used to simulate the MindToSound middleware
        public void StartMindToSoundSimulation(List<bool> bandFlagsList, List<bool> sensorFlagsList)
        {
            _transmission = true;
            string line = "";

            // Create the OSC transmitter
            _oscTransmitter = new OSC(_ipAddress, _portNum);

            // Skip the header row
            _index = 1;

            do
            {
                // Get a line of data
                line = GetNextCSVLine();
                if (line == "EOF")
                    break;

                // Get the data entries
                string[] dataEntries = BreakupDataRow(line);

                // Iterate through the data
                for (int i = 1; i <= (dataEntries.Length - 2); i++)
                {
                    if (i >= dataEntries.Length)
                        break;

                    // Construct the arguments
                    float floatValue = Convert.ToSingle(dataEntries[i]);
                    object[] args = { floatValue };

                    // Send the OSC message
                    _oscTransmitter.SendMessage(GetOSCAddress(i), args);
                }

                Thread.Sleep(Convert.ToInt32(_transmissionDelay * 1000));
            }
            while (line != "EOF" && _transmission);
        }

        // Method for zero-ing out the OSC channels
        public async Task StopMindToSoundSimulation()
        {

            _transmission = false;
            await Task.Delay(100);

            await ZeroOSCChannels();

            // Close the OSC transmitter
            _oscTransmitter.Close();
        }

        // Method for zerog out the OSC channels
        public async Task ZeroOSCChannels()
        {
            // Zero each channel
            foreach (string sensor in _sensors)
            {
                foreach (string band in _bands)
                {
                    object[] args = { 0f };
                    _oscTransmitter.SendMessage($"/{band}/{sensor}", args);
                }
                await Task.Delay(Convert.ToInt32(_transmissionDelay * 1000));
            }
        }

        // Method for getting the states
        private void ScrapeStates()
        {
            // Get the header row
            _index = 0;
            string line = GetNextCSVLine();
            string state;
            if (line != "EOF")
            {
                // Get the data entries
                string[] dataEntries = BreakupDataRow(line);

                // Get the state
                state = dataEntries[dataEntries.Length - 1];

                // Check if it is a states column
                if (state != "State")
                {
                    return;
                }

                _statesInUse = true;
            }

            do
            {
                // Get a line of data
                line = GetNextCSVLine();
                if (line == "EOF")
                    break;

                // Get the data entries
                string[] dataEntries = BreakupDataRow(line);

                // Get the state
                state = dataEntries[dataEntries.Length - 1];

                // Add the line index to the states index list
                switch (state)
                {
                    case "Baseline":
                        _baselineDataIndexs.Add(_index);
                        break;

                    case "Transition_to_TH":
                        _transitionToThDataIndexs.Add(_index);
                        break;

                    case "Transient_Hypofrontality":
                        _thDataIndexs.Add(_index);
                        break;

                    case "Transition_to_Flow":
                        _transitionToFlowDataIndexs.Add(_index);
                        break;

                    case "Flow":
                        _flowDataIndexs.Add(_index);
                        break;

                    case "MANUAL_REVIEW":
                        throw new Exception("Error: Manual review of some states required.");

                    default:
                        throw new Exception("Error: Please process the CSV first.");

                }
            }
            while (line != "EOF");
        }

        // Method to return which buttons can be enabled
        public bool[] GetButtonStates()
        {
            bool baseline = false, transitionTh = false, th = false, trasitionFlow = false, flow = false;

            if (_baselineDataIndexs.Count > 0)
            {
                baseline = true;
            }
            if (_transitionToThDataIndexs.Count > 0)
            {
                transitionTh = true;
            }
            if (_thDataIndexs.Count > 0)
            {
                th = true;
            }
            if (_transitionToFlowDataIndexs.Count > 0)
            {
                trasitionFlow = true;
            }
            if (_flowDataIndexs.Count > 0)
            {
                flow = true;
            }

            return [baseline, transitionTh, th, trasitionFlow, flow];
        }

        // Method used to send a certain states data
        public async Task PlaybackState(State desiredState)
        {
            switch (desiredState)
            {
                case State.Baseline:
                    await StateSimulation(_baselineDataIndexs);
                    break;
                case State.TransitionToTh:
                    await StateSimulation(_transitionToThDataIndexs);
                    break;
                case State.Th:
                    await StateSimulation(_thDataIndexs);
                    break;
                case State.TransitionToFlow:
                    await StateSimulation(_transitionToFlowDataIndexs);
                    break;
                case State.Flow:
                    await StateSimulation(_flowDataIndexs);
                    break;
            }
        }

        // Method for sending OSC data for a specific state
        private async Task StateSimulation(List<int> indexs)
        {
            _transmission = true;
            string line;

            // Iterate through the index list
            foreach (int index in indexs)
            {
                // Set the index
                _index = index;

                // Get a line of data
                line = GetNextCSVLine();
                if (line == "EOF")
                    break;

                // Get the data entries
                string[] dataEntries = BreakupDataRow(line);

                // Iterate through the data
                for (int i = 1; i <= (dataEntries.Length - 2); i++)
                {
                    if (i >= dataEntries.Length)
                        break;

                    // Construct the arguments
                    float floatValue = Convert.ToSingle(dataEntries[i]);
                    object[] args = { floatValue };

                    // Send the OSC message
                    _oscTransmitter.SendMessage(GetOSCAddress(i), args);
                }

                await Task.Delay(Convert.ToInt32(_transmissionDelay * 1000));
            }

            // Zero out the OSC channels
            await StopMindToSoundSimulation();
        }

        // Method for updating the IP address
        public void SetIpAddress(string ipAddress)
        {
            _ipAddress = ipAddress;
        }

        // Method for updating the port number
        public void SetPortNum(int portNum)
        {
            _portNum = portNum;
        }
    }
}
