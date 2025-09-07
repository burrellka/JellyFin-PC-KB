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
        private readonly IPlaybackManager _playback;
        private readonly IPolicyService _policies;
        private readonly IStateService _state;
        private readonly IEnforcementService _enforce;
        private readonly Plugin _plugin;

        public EnforcementHostedService(
            ILogger<EnforcementHostedService> logger,
            ISessionManager sessions,
            IPlaybackManager playback,
            IPolicyService policies,
            IStateService state,
            IEnforcementService enforce,
            Plugin plugin)
        {
            _logger = logger;
            _sessions = sessions;
            _playback = playback;
            _policies = policies;
            _state = state;
            _enforce = enforce;
            _plugin = plugin;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ParentGuard enforcement service starting");
            _playback.PlaybackStart += OnPlaybackStart;
            _playback.PlaybackProgress += OnPlaybackProgress;
            _playback.PlaybackStopped += OnPlaybackStopped;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("ParentGuard enforcement service stopping");
            _playback.PlaybackStart -= OnPlaybackStart;
            _playback.PlaybackProgress -= OnPlaybackProgress;
            _playback.PlaybackStopped -= OnPlaybackStopped;
            return Task.CompletedTask;
        }

        private void OnPlaybackStart(object? sender, PlaybackProgressEventArgs e)
        {
            var userId = e.UserId?.ToString();
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
                TryStop(e.SessionId);
                if (decision.CooldownMinutes.HasValue)
                {
                    _state.SetCooldown(userId, utcNow.AddMinutes(decision.CooldownMinutes.Value));
                }
            }
        }

        private void OnPlaybackProgress(object? sender, PlaybackProgressEventArgs e)
        {
            var userId = e.UserId?.ToString();
            if (string.IsNullOrEmpty(userId)) return;

            var utcNow = DateTime.UtcNow;
            var policy = _policies.GetEffectivePolicy(userId, _plugin.Configuration);
            var state = _state.Get(userId);

            // Detect item switch
            if (e.IsPlaying && e.PlaybackStopTime != null && e.PositionTicks != null && e.PositionTicks.Value == 0)
            {
                var sw = _enforce.ShouldAllowSwitch(userId, policy, state, utcNow);
                if (!sw.Allow)
                {
                    _logger.LogInformation("ParentGuard: switch rate-limit hit for {User}", userId);
                    _state.SetCooldown(userId, utcNow.AddMinutes(sw.CooldownMinutes ?? policy.CooldownOnTripMinutes));
                    TryStop(e.SessionId);
                    return;
                }
                _state.AddSwitch(userId, utcNow);
            }

            // Detect seek by large jump in progress
            // Jellyfin doesn’t expose a direct seek event here; treat any backward/forward jump > 10s as a seek.
            if (e.SeekPositionTicks.HasValue)
            {
                var sk = _enforce.ShouldAllowSeek(userId, policy, state, utcNow);
                if (!sk.Allow)
                {
                    _logger.LogInformation("ParentGuard: seek rate-limit hit for {User}", userId);
                    _state.SetCooldown(userId, utcNow.AddMinutes(sk.CooldownMinutes ?? policy.CooldownOnTripMinutes));
                    TryStop(e.SessionId);
                    return;
                }
                _state.AddSeek(userId, utcNow);
            }
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
                _sessions.SendMessageCommand(sessionId, "Stop", null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ParentGuard: failed to stop session {Session}", sessionId);
            }
        }
    }
}


