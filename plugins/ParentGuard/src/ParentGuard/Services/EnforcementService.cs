using System;
using System.Linq;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ParentGuard.Services
{
    public interface IEnforcementService
    {
        EnforcementDecision ShouldAllowPlaybackStart(string userId, ProfilePolicy policy, UserState state, DateTime utcNow, DateTime localNow, int dailyBudgetMinutes);
        EnforcementDecision ShouldAllowSeek(string userId, ProfilePolicy policy, UserState state, DateTime utcNow);
        EnforcementDecision ShouldAllowSwitch(string userId, ProfilePolicy policy, UserState state, DateTime utcNow);
    }

    public record EnforcementDecision(bool Allow, string? Reason = null, int? CooldownMinutes = null);

    public class EnforcementService : IEnforcementService
    {
        private readonly ILogger<EnforcementService> _logger;

        public EnforcementService(ILogger<EnforcementService> logger)
        {
            _logger = logger;
        }

        public EnforcementDecision ShouldAllowPlaybackStart(string userId, ProfilePolicy policy, UserState state, DateTime utcNow, DateTime localNow, int dailyBudgetMinutes)
        {
            if (state.CooldownUntilUtc.HasValue && state.CooldownUntilUtc.Value > utcNow)
            {
                return new EnforcementDecision(false, "cooldown");
            }

            if (!policy.Enabled)
            {
                return new EnforcementDecision(true);
            }

            // Schedule
            // Assumed checked by caller through PolicyService if needed; returning allow by default

            // Budget
            if (state.MinutesConsumed >= dailyBudgetMinutes)
            {
                return new EnforcementDecision(false, "daily_budget_exhausted");
            }

            return new EnforcementDecision(true);
        }

        public EnforcementDecision ShouldAllowSeek(string userId, ProfilePolicy policy, UserState state, DateTime utcNow)
        {
            // Trim window
            var cutoff = utcNow.AddMinutes(-policy.SeekRateLimit.WindowMinutes);
            state.SeekEvents.RemoveAll(t => t < cutoff);

            if (state.SeekEvents.Count >= policy.SeekRateLimit.MaxEvents)
            {
                return new EnforcementDecision(false, "seek_rate_limit", policy.CooldownOnTripMinutes);
            }
            return new EnforcementDecision(true);
        }

        public EnforcementDecision ShouldAllowSwitch(string userId, ProfilePolicy policy, UserState state, DateTime utcNow)
        {
            var cutoff = utcNow.AddMinutes(-policy.SwitchRateLimit.WindowMinutes);
            state.SwitchEvents.RemoveAll(t => t < cutoff);
            if (state.SwitchEvents.Count >= policy.SwitchRateLimit.MaxEvents)
            {
                return new EnforcementDecision(false, "switch_rate_limit", policy.CooldownOnTripMinutes);
            }
            return new EnforcementDecision(true);
        }
    }
}


