using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Timers;

namespace CortexAccess
{
    public class HeadsetFinder
    {
        private CortexClient _ctxClient;
        private string _wantedHeadsetId; // headset id of wanted headset device
        private Timer _aTimer;
        private Utils _utitilies = new Utils();

        private bool _hasHeadsetConnected;
        private bool _isHeadsetScanning = false;
        private bool _isAutoConnect = true; // set to false if you don't want to connect automatically to a headset

        // Event
        public event EventHandler<string> OnHeadsetConnected;

        public bool IsHeadsetScanning { get => _isHeadsetScanning; }
        public bool HasHeadsetConnected { get => _hasHeadsetConnected; set => _hasHeadsetConnected = value; }
        public bool IsAutoConnect { get => _isAutoConnect; set => _isAutoConnect = value; }

        public HeadsetFinder()
        {
            _ctxClient = CortexClient.Instance;
            _wantedHeadsetId = "";
            _hasHeadsetConnected = false;
            _ctxClient.OnQueryHeadset += QueryHeadsetOK;
            _ctxClient.HeadsetConnectNotify += OnHeadsetConnectNotify;
            _ctxClient.HeadsetScanFinished += OnHeadsetScanFinished;
        }

        public void FindHeadset(string wantedHeadsetId = "")
        {
            // Check for a connected headset
            if (!_hasHeadsetConnected)
            {
                _wantedHeadsetId = wantedHeadsetId;
                SetTimer(); // set timer for query headset
                _ctxClient.QueryHeadsets(wantedHeadsetId);
            }
        }

        /// <summary>
        /// ScanHeadsets to trigger scan headsets from Cortex
        /// </summary>
        public void ScanHeadsets()
        {
            _isHeadsetScanning = true;
            _ctxClient.ControlDevice("refresh", "", new JObject());
        }

        private void OnHeadsetScanFinished(object sender, string message)
        {
            _isHeadsetScanning = false;
        }

        private void OnHeadsetConnectNotify(object sender, HeadsetConnectEventArgs e)
        {
            string headsetId = e.HeadsetId;
            if (headsetId == _wantedHeadsetId)
            {
                if (e.IsSuccess)
                {
                    OnHeadsetConnected(this, _wantedHeadsetId);
                    _hasHeadsetConnected = true;
                }
                else
                {
                    _hasHeadsetConnected = false;
                    _utitilies.SendErrorMessage("Connecting to headset " + headsetId + ", Message: " + e.Message);
                }
            }
        }

        private void QueryHeadsetOK(object sender, List<Headset> headsets)
        {
            if (headsets.Count > 0 && !_hasHeadsetConnected)
            {
                Headset _wantedHeadset = new Headset();
                foreach (var headsetItem in headsets)
                {
                    if (!String.IsNullOrEmpty(_wantedHeadsetId) && _wantedHeadsetId == headsetItem.HeadsetID)
                    {
                        _wantedHeadset = headsetItem;
                    }
                }

                if (String.IsNullOrEmpty(_wantedHeadsetId))
                {
                    // set wanted headset is first headset
                    _wantedHeadset = headsets.First<Headset>();
                    _wantedHeadsetId = _wantedHeadset.HeadsetID;
                }

                if (_wantedHeadset.Status == "discovered")
                {
                    // prepare flex mapping if the headset is EPOC Flex
                    JObject flexMappings = new JObject();
                    if (_wantedHeadset.HeadsetID.IndexOf("FLEX", StringComparison.OrdinalIgnoreCase) > 0)
                    {
                        // For an Epoc Flex headset, we need a mapping
                        flexMappings = JObject.Parse(Config.FlexMapping);
                    }
                    _ctxClient.ControlDevice("connect", _wantedHeadset.HeadsetID, flexMappings);
                }
                else if (_wantedHeadset.Status == "connected")
                {
                    OnHeadsetConnected(this, _wantedHeadsetId);
                    _hasHeadsetConnected = true;
                }
            }
            else
            {
                _utitilies.SendErrorMessage("No headsets available. Please connect a headset to the machine via EMOTIV Launcher");
            }
        }

        // Create Timer for headset finding
        private void SetTimer()
        {
            // Create a timer with 5 seconds
            _aTimer = new Timer(5000);

            // Hook up the Elapsed event for the timer. 
            _aTimer.Elapsed += OnTimedEvent;
            _aTimer.AutoReset = true;
            _aTimer.Enabled = true;
        }

        private void OnTimedEvent(object sender, ElapsedEventArgs e)
        {
            if (!_hasHeadsetConnected && _isAutoConnect)
            {
                // Query headset again
                _ctxClient.QueryHeadsets("");
            }
        }
    }
}
