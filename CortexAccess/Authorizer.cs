using System;

namespace CortexAccess
{
    public class Authorizer
    {
        private CortexClient _ctxClient;
        private Utils _utilities = new Utils();
        private string _cortexToken;
        private string _emotivId;
        private bool _isEulaAccepted;
        private bool _hasAccessRight;
        private string _licenseID;
        private ushort _debitNo = 5; // default value

        // Event
        public event EventHandler<string> OnAuthorized;

        // Constructor
        public Authorizer()
        {
            _ctxClient = CortexClient.Instance;
            _cortexToken = "";
            _emotivId = "";
            _isEulaAccepted = false;
            _hasAccessRight = false;

            _ctxClient.OnConnected += ConnectedOK;
            _ctxClient.OnGetUserLogin += GetUserLoginOK;
            _ctxClient.OnUserLogin += UserLoginOK; // inform user loggin 
            _ctxClient.OnUserLogout += UserLogoutOK; // inform user log out
            _ctxClient.OnHasAccessRight += HasAccessRightOK;
            _ctxClient.OnRequestAccessDone += RequestAccessDone;
            _ctxClient.OnAccessRightGranted += AccessRightGrantedOK; // inform user have granted or rejected access right for the App
            _ctxClient.OnAuthorize += AuthorizedOK;
            _ctxClient.OnEULAAccepted += EULAAcceptedOK;

        }

        private void ConnectedOK(object sender, bool isConnected)
        {
            if (isConnected)
            {
                _ctxClient.GetUserLogin();
            }
            else
            {
                _utilities.SendErrorMessage("Can not connect to Cortex, please restart Cortex.");
            }
        }

        private void EULAAcceptedOK(object sender, bool isEULAAccepted)
        {
            _isEulaAccepted = isEULAAccepted;
            if (isEULAAccepted)
            {
                _ctxClient.Authorize(_licenseID, _debitNo);
            }
            else
            {
                _utilities.SendErrorMessage("User has not accepted EULA, please accept on EMOTIV Launcher to proceed.");
            }
        }

        private void AuthorizedOK(object sender, string cortexToken)
        {
            if (!String.IsNullOrEmpty(cortexToken))
            {
                _cortexToken = cortexToken;
                _isEulaAccepted = true;
                OnAuthorized(this, _cortexToken);
            }
            else
            {
                _isEulaAccepted = false;
                _utilities.SendErrorMessage("User has not accepted EULA, please accept on EMOTIV Launcher to proceed.");
            }
        }

        private void AccessRightGrantedOK(object sender, bool isGranted)
        {
            if (isGranted)
            {
                if (String.IsNullOrEmpty(_cortexToken))
                {
                    _ctxClient.Authorize(_licenseID, _debitNo);
                }
            }
            else
            {
                _utilities.SendErrorMessage("The access right to the application has been rejected, please try again.");
            }
        }

        private void RequestAccessDone(object sender, bool hasAccessRight)
        {
            if (!hasAccessRight)
            {
                _utilities.SendErrorMessage("Access has not been granted to this application. Please use the EMOTIV Launcher to proceed.");
            }
        }

        private void HasAccessRightOK(object sender, bool hasAccessRight)
        {
            if (hasAccessRight)
            {
                _ctxClient.Authorize(_licenseID, _debitNo);
            }
            else
            {
                _ctxClient.RequestAccess();
            }
        }

        private void UserLogoutOK(object sender, string message)
        {
            _utilities.SendSuccessMessage(message);
            _emotivId = "";
            _cortexToken = "";
            _isEulaAccepted = false;
            _hasAccessRight = false;
        }

        private void UserLoginOK(object sender, string message)
        {
            if (String.IsNullOrEmpty(EmotivId))
            {
                _ctxClient.GetUserLogin();
            }
        }

        private void GetUserLoginOK(object sender, string emotivId)
        {
            if (!String.IsNullOrEmpty(emotivId))
            {
                _emotivId = emotivId;
                _ctxClient.HasAccessRights();
            }
            else
            {
                _utilities.SendErrorMessage("Please login via EMOTIV Launcher before working with Cortex");
            }

        }



        // Property
        public string CortexToken
        {
            get
            {
                return _cortexToken;
            }
        }

        public string EmotivId
        {
            get
            {
                return _emotivId;
            }
        }

        public bool IsEulaAccepted
        {
            get
            {
                return _isEulaAccepted;
            }
        }

        public bool HasAccessRight
        {
            get
            {
                return _hasAccessRight;
            }
        }

        // Start
        public bool Start(string licenseID = "")
        {
            _licenseID = licenseID;
            return _ctxClient.Open();
        }
    }
}
