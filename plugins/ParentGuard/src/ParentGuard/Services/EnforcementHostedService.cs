using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ParentGuard.Services
{
    // 10.10-compatible hosted service: subscribes to playback events and enforces seek/switch/cooldown.
    public class EnforcementHostedService : IHostedService
    {
        private readonly ILogger<EnforcementHostedService> _logger;
        private readonly ISessionManager _sessions;
        private readonly IPolicyService _policies;
        private readonly IStateService _state;
        private readonly IEnforcementService _enforce;
        private readonly Plugin _plugin;

        public EnforcementHostedService(
            ILogger<EnforcementHostedService> logger,
            ISessionManager sessions,
            IPolicyService policies,
            IStateService state,
            IEnforcementService enforce,
            Plugin plugin)
        {
            _logger = logger;
            _sessions = sessions;
            _policies = policies;
            _state = state;
            _enforce = enforce;
            _plugin = plugin;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ParentGuard enforcement service starting");
            _sessions.PlaybackStart += OnPlaybackStart;
            _sessions.PlaybackProgress += OnPlaybackProgress;
            _sessions.PlaybackStopped += OnPlaybackStopped;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ParentGuard enforcement service stopping");
            _sessions.PlaybackStart -= OnPlaybackStart;
            _sessions.PlaybackProgress -= OnPlaybackProgress;
            _sessions.PlaybackStopped -= OnPlaybackStopped;
            return Task.CompletedTask;
        }

        private void OnPlaybackStart(object? sender, PlaybackProgressEventArgs e)
        {
            var userId = e.Session?.UserId?.ToString();
            if (string.IsNullOrEmpty(userId)) return;
            var policy = _policies.GetEffectivePolicy(userId, _plugin.Configuration);
            var state = _state.Get(userId);
            var utcNow = DateTime.UtcNow;
            var localNow = DateTime.Now;
            var budget = _policies.GetDailyBudget(policy, localNow.DayOfWeek);
            var decision = _enforce.ShouldAllowPlaybackStart(userId, policy, state, utcNow, localNow, budget);
            if (!decision.Allow)
            {
                _logger.LogInformation("ParentGuard: blocking start for {User} due to {Reason}", userId, decision.Reason);
                TryStop(e.Session?.Id);
                if (decision.CooldownMinutes.HasValue)
                {
                    _state.SetCooldown(userId, utcNow.AddMinutes(decision.CooldownMinutes.Value));
                }
            }
        }

        private void OnPlaybackProgress(object? sender, PlaybackProgressEventArgs e)
        {
            // For now, only track time consumption on progress at 60s boundaries; seek/switch enforcement will come later
            var userId = e.Session?.UserId?.ToString();
            if (string.IsNullOrEmpty(userId)) return;
            _state.AddMinutes(userId, 1, DateTime.UtcNow);
        }

        private void OnPlaybackStopped(object? sender, PlaybackStopEventArgs e)
        {
            // No-op for now
        }

        private void TryStop(string? sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return;
            try
            {
                _sessions.SendMessageCommand(sessionId, "Stop", null, default);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ParentGuard: failed to stop session {Session}", sessionId);
            }
        }
    }
}


