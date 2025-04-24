using System;
using System.Threading;
using WebSocket4Net;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;

namespace CortexAccess
{
    public enum SessionStatus
    {
        Opened = 0,
        Activated = 1,
        Closed = 2
    }

    // Event for subscribe and unsubscribe
    public class MultipleResultEventArgs
    {
        public MultipleResultEventArgs(JArray successList, JArray failList)
        {
            SuccessList = successList;
            FailList = failList;
        }
        public JArray SuccessList { get; set; }
        public JArray FailList { get; set; }
    }

    // Event for createSession and updateSession
    public class SessionEventArgs
    {
        public SessionEventArgs(string sessionId, string status, string appId)
        {
            SessionId = sessionId;
            ApplicationId = appId;

            switch (status)
            {
                case "opened":
                    Status = SessionStatus.Opened;
                    break;
                case "activated":
                    Status = SessionStatus.Activated;
                    break;
                default:
                    Status = SessionStatus.Closed;
                    break;
            }
        }

        public string SessionId { get; set; }
        public SessionStatus Status { get; set; }
        public string ApplicationId { get; set; }
    }
    public class StreamDataEventArgs
    {
        public StreamDataEventArgs(string sid, JArray data, double time, string streamName)
        {
            Sid = sid;
            Time = time;
            Data = data;
            StreamName = streamName;
        }
        public string Sid { get; private set; } // subscription id
        public double Time { get; private set; }
        public JArray Data { get; private set; }
        public string StreamName { get; private set; }
    }
    public class ErrorMsgEventArgs
    {
        public ErrorMsgEventArgs(int code, string messageError)
        {
            Code = code;
            MessageError = messageError;
        }
        public int Code { get; set; }
        public string MessageError { get; set; }
    }

    // event to inform about headset connect
    public class HeadsetConnectEventArgs
    {
        public HeadsetConnectEventArgs(bool isSuccess, string message, string headsetId)
        {
            IsSuccess = isSuccess;
            Message = message;
            HeadsetId = headsetId;
        }
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public string HeadsetId { get; set; }
    }

    public sealed class CortexClient
    {
        const string Url = "wss://localhost:6868";
        private string m_CurrentMessage = string.Empty;
        private Dictionary<int, string> _methodForRequestId;

        private WebSocket _wSC; // Websocket Client
        private int _nextRequestId; // Unique id for each request
        private bool _isWSConnected;

        private Utils _utilities = new Utils();

        //Events
        private AutoResetEvent m_MessageReceiveEvent = new AutoResetEvent(false);
        private AutoResetEvent m_OpenedEvent = new AutoResetEvent(false);
        private AutoResetEvent m_CloseEvent = new AutoResetEvent(false);

        public event EventHandler<bool> OnConnected;
        public event EventHandler<ErrorMsgEventArgs> OnErrorMsgReceived;
        public event EventHandler<StreamDataEventArgs> OnStreamDataReceived;
        public event EventHandler<List<Headset>> OnQueryHeadset;
        public event EventHandler<bool> OnHasAccessRight;
        public event EventHandler<bool> OnRequestAccessDone;
        public event EventHandler<bool> OnAccessRightGranted;
        public event EventHandler<string> OnAuthorize;
        public event EventHandler<string> OnGetUserLogin;
        public event EventHandler<bool> OnEULAAccepted;
        public event EventHandler<string> OnUserLogin;
        public event EventHandler<string> OnUserLogout;
        public event EventHandler<SessionEventArgs> OnCreateSession;
        public event EventHandler<SessionEventArgs> OnUpdateSession;
        public event EventHandler<MultipleResultEventArgs> OnSubscribeData;
        public event EventHandler<MultipleResultEventArgs> OnUnSubscribeData;
        public event EventHandler<string> SessionClosedNotify;
        public event EventHandler<HeadsetConnectEventArgs> HeadsetConnectNotify;
        public event EventHandler<string> HeadsetScanFinished;

        // Constructor
        static CortexClient()
        {

        }
        private CortexClient()
        {
            _nextRequestId = 1;
            System.Net.ServicePointManager.ServerCertificateValidationCallback = (sender, certicate, chain, SslPolicyErrors) => true;
            _wSC = new WebSocket(Url);
            // Since Emotiv Cortex 3.7.0, the supported SSL Protocol will be TLS1.2 or later
            _wSC.Security.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12;

            _methodForRequestId = new Dictionary<int, string>();
            _wSC.Opened += new EventHandler(WebSocketClient_Opened);

            _wSC.Error += new EventHandler<SuperSocket.ClientEngine.ErrorEventArgs>(WebSocketClient_Error);

            _wSC.Closed += new EventHandler(WebSocketClient_Closed);

            _wSC.MessageReceived += new EventHandler<MessageReceivedEventArgs>(WebSocketClient_MessageReceived);
        }
        // Properties
        public static CortexClient Instance { get; } = new CortexClient();

        public bool IsWSConnected
        {
            get
            {
                return _isWSConnected;
            }
        }

        // Build a request message
        private void SendTextMessage(JObject param, string method, bool hasParam = true)
        {
            JObject request = new JObject(
            new JProperty("jsonrpc", "2.0"),
            new JProperty("id", _nextRequestId),
            new JProperty("method", method));

            if (hasParam)
            {
                request.Add("params", param);
            }

            // send the json message
            _wSC.Send(request.ToString());

            _methodForRequestId.Add(_nextRequestId, method);
            _nextRequestId++;
        }

        // Handle receieved message 
        private void WebSocketClient_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            m_CurrentMessage = e.Message;
            m_MessageReceiveEvent.Set();

            JObject response = JObject.Parse(e.Message);

            if (response["id"] != null)
            {
                int id = (int)response["id"];

                // Prevent the KeyNotFoundException happening on program shutdown
                if(_methodForRequestId.ContainsKey(id))
                {
                    string method = _methodForRequestId[id];
                    _methodForRequestId.Remove(id);

                    if (response["error"] != null)
                    {
                        JObject error = (JObject)response["error"];
                        int code = (int)error["code"];
                        string errorMessage = (string)error["message"];
                        _utilities.SendErrorMessage(errorMessage);

                        //Send Error message event
                        OnErrorMsgReceived(this, new ErrorMsgEventArgs(code, errorMessage));
                    }
                    else
                    {
                        // handle response
                        JToken data = response["result"];
                        HandleResponse(method, data);
                    }
                }
                else
                {
                    _utilities.SendWarningMessage("Received response for unknown request ID: " + id);
                }
            }
            else if (response["sid"] != null)
            {
                string sid = (string)response["sid"];
                double time = 0;
                if (response["time"] != null)
                    time = (double)response["time"];

                foreach (JProperty property in response.Properties())
                {
                    if (property.Name != "sid" &&
                        property.Name != "time")
                    {
                        OnStreamDataReceived(this, new StreamDataEventArgs(sid, (JArray)property.Value, time, property.Name));
                    }
                }
            }
            else if (response["warning"] != null)
            {
                JObject warning = (JObject)response["warning"];
                int code = -1;
                if (warning["code"] != null)
                {
                    code = (int)warning["code"];
                }
                JToken messageData = warning["message"];
                HandleWarning(code, messageData);
            }
        }
        // handle Response
        private void HandleResponse(string method, JToken data)
        {
            switch(method)
            {
                case "queryHeadsets":
                    List<Headset> headsetLists = new List<Headset>();
                    foreach(JObject item in data)
                    {
                        headsetLists.Add(new Headset(item));
                    }
                    OnQueryHeadset(this, headsetLists);
                    break;

                case "getUserLogin":
                    JArray users = (JArray)data;
                    string username = "";
                    if (users.Count > 0)
                    {
                        foreach (JObject user in users)
                        {
                            if (user["currentOSUId"].ToString() == user["loggedInOSUId"].ToString())
                            {
                                username = user["username"].ToString();
                            }
                        }
                    }
                    OnGetUserLogin(this, username);
                    break;

                case "hasAccessRight":
                    OnHasAccessRight(this, (bool)data["accessGranted"]);
                    break;

                case "requestAccess":
                    OnRequestAccessDone(this, (bool)data["accessGranted"]);
                    break;

                case "authorize":
                    string token = (string)data["cortexToken"];
                    bool eulaAccepted = true;
                    if (data["warning"] != null)
                    {
                        JObject warning = (JObject)data["warning"];
                        eulaAccepted = !((int)warning["code"] == WarningCode.UserNotAcceptLicense);
                        token = "";
                    }
                    OnAuthorize(this, token);
                    break;

                case "createSession":
                    OnCreateSession(this, new SessionEventArgs (
                        (string)data["id"],
                        (string)data["status"],
                        (string)data["appId"]
                    ));
                    break;

                case "updateSession":
                    OnUpdateSession(this, new SessionEventArgs(
                        (string)data["id"],
                        (string)data["status"],
                        (string)data["appId"]
                    ));
                    break;

                case "unsubscribe":
                    OnUnSubscribeData(this, new MultipleResultEventArgs(
                        (JArray)data["success"],
                        (JArray)data["failure"]
                    ));
                    break;

                case "subscribe":
                    OnSubscribeData(this, new MultipleResultEventArgs(
                        (JArray)data["success"],
                        (JArray)data["failure"]
                    ));
                    break;
            }
        }

        // handle warning response
        private void HandleWarning(int code, JToken messageData)
        {
            //_utilities.SendWarningMessage(code.ToString(), false);

            switch (code)
            {
                case WarningCode.StreamStop:
                case WarningCode.SessionAutoClosed:
                    SessionClosedNotify(this, messageData["sessionId"].ToString());
                    break;

                case WarningCode.UserLogin:
                    OnUserLogin(this, messageData.ToString());
                    break;

                case WarningCode.UserLogout:
                    OnUserLogout(this, messageData.ToString());
                    break;

                case WarningCode.AccessRightGranted:
                    OnAccessRightGranted(this, true);
                    break;

                case WarningCode.AccessRightRejected:
                    OnAccessRightGranted(this, false);
                    break;

                case WarningCode.EULAAccepted:
                    OnEULAAccepted(this, true);
                    break;

                case WarningCode.HeadsetWrongInformation:
                case WarningCode.HeadsetCannotConnected:
                case WarningCode.HeadsetConnectingTimeout:
                    HeadsetConnectNotify(this, new HeadsetConnectEventArgs(
                        false,
                        messageData["behavior"].ToString(),
                        messageData["headsetId"].ToString()
                    ));
                    break;

                case WarningCode.HeadsetConnected:
                    HeadsetConnectNotify(this, new HeadsetConnectEventArgs(
                        true,
                        messageData["behavior"].ToString(),
                        messageData["headsetId"].ToString()
                    ));
                    break;

                case WarningCode.HeadsetScanFinished:
                    HeadsetScanFinished(this, messageData["behavior"].ToString());
                    break;
            }
        }

        private void WebSocketClient_Closed(object sender, EventArgs e)
        {
            m_CloseEvent.Set();
        }

        private void WebSocketClient_Opened(object sender, EventArgs e)
        {
            m_OpenedEvent.Set();
        }

        private void WebSocketClient_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            _utilities.SendErrorMessage(e.Exception.Message + Environment.NewLine + e.Exception.StackTrace);

            if (e.Exception.InnerException != null)
            {
                Console.WriteLine(e.Exception.InnerException.GetType());
            }
        }


        //Open socket
        public bool Open()
        {
            //Open websocket
            try
            {
                _wSC.Open();
            }
            catch
            {
                return false;
            }

            if (!m_OpenedEvent.WaitOne(10000))
            {
                return false;
            }

            if (_wSC.State == WebSocketState.Open)
            {
                _isWSConnected = true;
                OnConnected(this, true);
            }
            else
            {
                _isWSConnected = false;
                OnConnected(this, false);
            }

            return true;
        }

        // Close socket
        public bool Close()
        {
            // Close the websocket
            try
            {
                _wSC.Close();
            }
            catch
            {
                return false;
            }

            return true;
        }

        // Has Access Right
        public void HasAccessRights()
        {
            JObject param = new JObject(
                    new JProperty("clientId", Config.AppClientId),
                    new JProperty("clientSecret", Config.AppClientSecret)
                );
            SendTextMessage(param, "hasAccessRight", true);
        }
        // Request Access
        public void RequestAccess()
        {
            JObject param = new JObject(
                    new JProperty("clientId", Config.AppClientId),
                    new JProperty("clientSecret", Config.AppClientSecret)
                );
            SendTextMessage(param, "requestAccess", true);
        }
        // Authorize
        public void Authorize(string licenseID, int debitNumber)
        {
            JObject param = new JObject();
            param.Add("clientId", Config.AppClientId);
            param.Add("clientSecret", Config.AppClientSecret);
            if (!string.IsNullOrEmpty(licenseID))
            {
                param.Add("license", licenseID);
            }
            param.Add("debit", debitNumber);
            SendTextMessage(param, "authorize", true);
        }

        // GetUserLogin
        public void GetUserLogin()
        {
            JObject param = new JObject();
            SendTextMessage(param, "getUserLogin", false);
        }
        // GenerateNewToken
        public void GenerateNewToken(string currentAccessToken)
        {
            JObject param = new JObject(
                    new JProperty("clientId", Config.AppClientId),
                    new JProperty("clientSecret", Config.AppClientSecret),
                    new JProperty("token", currentAccessToken)
                );
            SendTextMessage(param, "generateNewToken", true);
        }

        // QueryHeadset
        public void QueryHeadsets(string headsetId)
        {
            JObject param = new JObject();
            if (!String.IsNullOrEmpty(headsetId))
            {
                param.Add("id", headsetId);
            }
            SendTextMessage(param, "queryHeadsets", false);
        }

        // controlDevice
        // required params: command
        // command = {"connect", "disconnect", "refresh"}
        // mappings is required if connect to epoc flex
        public void ControlDevice(string command, string headsetId, JObject mappings)
        {
            JObject param = new JObject();
            param.Add("command", command);
            if (!String.IsNullOrEmpty(headsetId))
            {
                param.Add("headset", headsetId);
            }
            if (mappings.Count > 0)
            {
                param.Add("mappings", mappings);
            }
            SendTextMessage(param, "controlDevice", true);
        }


        // CreateSession
        // Required params: cortexToken, status
        public void CreateSession(string cortexToken, string headsetId, string status)
        {
            JObject param = new JObject();
            if (!String.IsNullOrEmpty(headsetId))
            {
                param.Add("headset", headsetId);
            }
            param.Add("cortexToken", cortexToken);
            param.Add("status", status);
            SendTextMessage(param, "createSession", true);
        }

        // UpdateSession
        // Required params: session, status, cortexToken
        public void UpdateSession(string cortexToken, string sessionId, string status)
        {
            JObject param = new JObject();
            param.Add("session", sessionId);
            param.Add("cortexToken", cortexToken);
            param.Add("status", status);
            SendTextMessage(param, "updateSession", true);
        }


        // Subscribe Data
        // Required params: session, cortexToken, streams
        public void Subscribe(string cortexToken, string sessionId, List<string> streams)
        {
            JObject param = new JObject();
            param.Add("session", sessionId);
            param.Add("cortexToken", cortexToken);
            param.Add("streams", JToken.FromObject(streams));
            SendTextMessage(param, "subscribe", true);
        }

        // UnSubscribe Data
        // Required params: session, cortexToken, streams
        public void UnSubscribe(string cortexToken, string sessionId, List<string> streams)
        {
            JObject param = new JObject();
            param.Add("session", sessionId);
            param.Add("cortexToken", cortexToken);
            param.Add("streams", JToken.FromObject(streams));
            SendTextMessage(param, "unsubscribe", true);
        }
    }
}
