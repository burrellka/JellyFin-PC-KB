using System;
using Microsoft.Extensions.DependencyInjection;
using MediaBrowser.Controller.Plugins;
using Jellyfin.Plugin.ParentGuard.Services;
using MediaBrowser.Controller;

namespace Jellyfin.Plugin.ParentGuard
{
    public class Startup : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection services, IServerApplicationHost applicationHost)
        {
            services.AddSingleton<IPolicyService, PolicyService>();
            services.AddSingleton<IStateService, StateService>();
            services.AddSingleton<IEnforcementService, EnforcementService>();
            services.AddSingleton<IRequestsStore, RequestsStore>();
            services.AddHostedService<DailyResetService>();
            services.AddHostedService<EnforcementHostedService>();
        }
    }
}
