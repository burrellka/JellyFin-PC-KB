using Microsoft.Extensions.DependencyInjection;
using Jellyfin.Plugin.ParentGuard.Services;

namespace Jellyfin.Plugin.ParentGuard
{
    public static class StartupServices
    {
        // Helper for manual DI setup if Jellyfin loads with reflection
        public static void AddParentGuardServices(this IServiceCollection services)
        {
            services.AddSingleton<IPolicyService, PolicyService>();
            services.AddSingleton<IStateService, StateService>();
            services.AddSingleton<IEnforcementService, EnforcementService>();
            services.AddSingleton<IRequestsStore, RequestsStore>();
            services.AddHostedService<DailyResetService>();
        }
    }
}


