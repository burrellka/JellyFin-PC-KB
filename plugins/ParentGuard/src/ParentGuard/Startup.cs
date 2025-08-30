// Temporarily disabled until correct Jellyfin 10.10 startup types are confirmed
#if false
using System;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Jellyfin.Plugin.ParentGuard.Services;

namespace Jellyfin.Plugin.ParentGuard
{
    public class Startup : IPluginStartup
    {
        public void ConfigureServices(IServiceCollection services, IServerApplicationHost applicationHost)
        {
            services.AddSingleton<ServerEntryPoint>();
            services.AddSingleton<IPolicyService, PolicyService>();
            services.AddSingleton<IStateService, StateService>();
            services.AddSingleton<IEnforcementService, EnforcementService>();
            services.AddSingleton<IRequestsStore, RequestsStore>();
        }
    }
}
#endif


