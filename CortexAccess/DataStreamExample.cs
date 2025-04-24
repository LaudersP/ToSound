using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;

namespace CortexAccess
{
    public class DataStreamExample
    {
        private CortexClient _ctxClient;
        private List<string> _streams;
        private string _cortexToken;
        private string _sessionId;
        private bool _isActiveSession;

        private HeadsetFinder _headsetFinder;
        private Authorizer _authorizer;
        private SessionCreator _sessionCreator;
        private string _wantedHeadsetId;
        private Utils _utilities = new Utils();

        public List<string> Streams
        {
            get
            {
                return _streams;
            }

            set
            {
                _streams = value;
            }
        }

        public string SessionId
        {
            get
            {
                return _sessionId;
            }
        }

        // Event
        public event EventHandler<ArrayList> OnMotionDataReceived; // motion data
        public event EventHandler<ArrayList> OnEEGDataReceived; // eeg data
        //public event EventHandler<ArrayList> OnDevDataReceived; // contact quality data
        public event EventHandler<ArrayList> OnPerfDataReceived; // performance metric
        public event EventHandler<ArrayList> OnBandPowerDataReceived; // band power
        public event EventHandler<Dictionary<string, JArray>> OnSubscribed;

        // Constructor
        public DataStreamExample()
        {

            _authorizer = new Authorizer();
            _headsetFinder = new HeadsetFinder();
            _sessionCreator = new SessionCreator();
            _cortexToken = "";
            _sessionId = "";
            _isActiveSession = false;

            _streams = new List<string>();
            // Event register
            _ctxClient = CortexClient.Instance;
            _ctxClient.OnErrorMsgReceived += MessageErrorRecieved;
            _ctxClient.OnStreamDataReceived += StreamDataReceived;
            _ctxClient.OnSubscribeData += SubscribeDataOK;
            _ctxClient.OnUnSubscribeData += UnSubscribeDataOK;
            _ctxClient.SessionClosedNotify += SessionClosedOK;

            _authorizer.OnAuthorized += AuthorizedOK;
            _headsetFinder.OnHeadsetConnected += HeadsetConnectedOK;
            _sessionCreator.OnSessionCreated += SessionCreatedOk;
            _sessionCreator.OnSessionClosed += SessionClosedOK;
        }

        private void SessionClosedOK(object sender, string sessionId)
        {
            if (sessionId == _sessionId)
            {
                _utilities.SendSuccessMessage("The session " + sessionId + " has closed successfully...");
                _sessionId = "";
                _headsetFinder.HasHeadsetConnected = false;
            }
        }

        private void UnSubscribeDataOK(object sender, MultipleResultEventArgs e)
        {
            foreach (JObject ele in e.SuccessList)
            {
                string streamName = (string)ele["streamName"];
                if (_streams.Contains(streamName))
                {
                    _streams.Remove(streamName);
                }
            }

            foreach (JObject ele in e.FailList)
            {
                string streamName = (string)ele["streamName"];
                int code = (int)ele["code"];
                string errorMessage = (string)ele["message"];
                _utilities.SendErrorMessage("Unable to unsubscribe from " + streamName + ". Code: " + code + ", Message: " + errorMessage);
            }
        }

        private void SubscribeDataOK(object sender, MultipleResultEventArgs e)
        {
            foreach (JObject ele in e.FailList)
            {
                string streamName = (string)ele["streamName"];
                int code = (int)ele["code"];
                string errorMessage = (string)ele["message"];
                _utilities.SendErrorMessage("Unable to unsubscribe from " + streamName + ". Code: " + code + ", Message: " + errorMessage);
                if (_streams.Contains(streamName))
                {
                    _streams.Remove(streamName);
                }
            }

            Dictionary<string, JArray> header = new Dictionary<string, JArray>();
            foreach (JObject ele in e.SuccessList)
            {
                string streamName = (string)ele["streamName"];
                JArray cols = (JArray)ele["cols"];
                header.Add(streamName, cols);
            }

            if (header.Count > 0)
            {
                OnSubscribed(this, header);
            }
            else
            {
                _utilities.SendErrorMessage("No available subscribe stream(s), please try again.");
            }
        }

        private void SessionCreatedOk(object sender, string sessionId)
        {
            _sessionId = sessionId;
            string streamsString = string.Join(", ", Streams);

            _utilities.SendSuccessMessage("Connected to Band Power Logger!");
            Console.WriteLine("\nPress Esc to end program and exit\n");

            // subscribe
            _ctxClient.Subscribe(_cortexToken, _sessionId, Streams);
        }

        private void HeadsetConnectedOK(object sender, string headsetId)
        {
            _utilities.SendSuccessMessage("Successfully connected to \'" + headsetId + "\'!");

            // Wait a moment before creating session
            System.Threading.Thread.Sleep(1500);

            // CreateSession
            _sessionCreator.Create(_cortexToken, headsetId, _isActiveSession);
        }

        private void AuthorizedOK(object sender, string cortexToken)
        {
            if (!String.IsNullOrEmpty(cortexToken))
            {
                _cortexToken = cortexToken;
                if (!_headsetFinder.IsHeadsetScanning)
                {
                    // Start scanning headset. It will call one time whole program.
                    // If you want re-scan, please check IsHeadsetScanning and call ScanHeadsets() again
                    _headsetFinder.ScanHeadsets();
                }
                // find headset
                _headsetFinder.FindHeadset(_wantedHeadsetId);
            }
        }

        private void StreamDataReceived(object sender, StreamDataEventArgs e)
        {
            ArrayList data = e.Data.ToObject<ArrayList>();

            // insert timestamp to datastream
            data.Insert(0, e.Time);

            switch(e.StreamName)
            {
                case "eeg":
                    OnEEGDataReceived(this, data); 
                    break;

                case "mot":
                    OnMotionDataReceived(this, data);
                    break;

                case "met":
                    OnPerfDataReceived(this, data);
                    break;

                case "pow":
                    OnBandPowerDataReceived(this, data);
                    break;
            }
        }
        private void MessageErrorRecieved(object sender, ErrorMsgEventArgs e)
        {
            _utilities.SendErrorMessage("Code: " + e.Code + ", Message: " + e.MessageError);
        }

        // set Streams
        public void AddStreams(string stream)
        {
            if (!_streams.Contains(stream))
            {
                _streams.Add(stream);
            }
        }
        // start
        public bool Start(string licenseID = "", bool activeSession = false, string wantedHeadsetId = "")
        {
            _wantedHeadsetId = wantedHeadsetId;
            _isActiveSession = activeSession;
            return _authorizer.Start(licenseID);
        }

        // Unsubscribe
        public void UnSubscribe(List<string> streams = null)
        {
            if (streams == null)
            {
                // unsubscribe all data
                _ctxClient.UnSubscribe(_cortexToken, _sessionId, _streams);
            }
            else
                _ctxClient.UnSubscribe(_cortexToken, _sessionId, streams);
        }
        public bool CloseSession()
        {
            return _sessionCreator.CloseSession();
        }
    }
}
