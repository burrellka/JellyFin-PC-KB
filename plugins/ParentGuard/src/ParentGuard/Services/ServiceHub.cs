using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ParentGuard.Services
{
    public static class ServiceHub
    {
        private static readonly object _lock = new object();
        private static IPolicyService? _policies;
        private static IStateService? _state;
        private static IEnforcementService? _enforce;
        private static IRequestsStore? _requests;
        public static MediaBrowser.Controller.Library.IUserManager? UserManager { get; set; }

        public static IPolicyService Policies
        {
            get
            {
                if (_policies == null)
                {
                    lock (_lock)
                    {
                        _policies ??= new PolicyService();
                    }
                }
                return _policies;
            }
        }

        public static IStateService State
        {
            get
            {
                if (_state == null)
                {
                    lock (_lock)
                    {
                        _state ??= new StateService();
                    }
                }
                return _state;
            }
        }

        public static IEnforcementService Enforcement
        {
            get
            {
                if (_enforce == null)
                {
                    lock (_lock)
                    {
                        _enforce ??= new EnforcementService();
                    }
                }
                return _enforce;
            }
        }

        public static IRequestsStore Requests
        {
            get
            {
                if (_requests == null)
                {
                    lock (_lock)
                    {
                        _requests ??= new RequestsStore();
                    }
                }
                return _requests;
            }
        }
    }
}


