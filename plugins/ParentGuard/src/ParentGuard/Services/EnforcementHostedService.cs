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
        private readonly IUserManager _userManager;

        public EnforcementHostedService(
            ILogger<EnforcementHostedService> logger,
            ISessionManager sessions,
            IPolicyService policies,
            IStateService state,
            IEnforcementService enforce,
            Plugin plugin,
            IUserManager userManager)
        {
            _logger = logger;
            _sessions = sessions;
            _policies = policies;
            _state = state;
            _enforce = enforce;
            _plugin = plugin;
            _userManager = userManager;
            
            // Fallback for controllers
            ServiceHub.UserManager = userManager;
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
            var userId = e.Session != null ? e.Session.UserId.ToString() : null;
            if (string.IsNullOrEmpty(userId)) return;

            var policy = _policies.GetEffectivePolicy(userId, _plugin.Configuration);
            var state = _state.Get(userId);
            var utcNow = DateTime.UtcNow;
            var localNow = DateTime.Now;

            // 1. Check if this is a "Switch" (rapid episode change)
            // If the user stopped recently and is now starting a DIFFERENT item, it counts as a switch.
            // We ignore restarts of the same item (resume).
            if (state.LastStopUtc.HasValue && state.LastItemId.HasValue && e.Item != null)
            {
                // If ItemId changed...
                if (state.LastItemId.Value != e.Item.Id)
                {
                    // ...and it was recent
                    var timeSinceStop = utcNow - state.LastStopUtc.Value;
                    // Use the window from policy, or a default small window (e.g. 5 mins) to define "rapid" switching
                    // Actually, the policy defines the rate limit window. We should count it if it falls within that window.
                    // For simplicity, we just log the switch event now, and let ShouldAllowSwitch check the rate limit.
                    _state.AddSwitch(userId, utcNow);
                    
                    var switchDecision = _enforce.ShouldAllowSwitch(userId, policy, state, utcNow);
                    if (!switchDecision.Allow)
                    {
                         _logger.LogInformation("ParentGuard: blocking switch for {User} due to {Reason}", userId, switchDecision.Reason);
                        TryStop(e.Session?.Id);
                        if (switchDecision.CooldownMinutes.HasValue)
                        {
                            _state.SetCooldown(userId, utcNow.AddMinutes(switchDecision.CooldownMinutes.Value));
                        }
                        return; // Stop here
                    }
                }
            }

            // 2. Check Start/Budget
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
            
            // Reset seek tracking on start
            state.LastPositionTicks = e.Session?.PlayState?.PositionTicks;
            state.LastProgressUtc = utcNow;
        }

        private void OnPlaybackProgress(object? sender, PlaybackProgressEventArgs e)
        {
            var userId = e.Session != null ? e.Session.UserId.ToString() : null;
            if (string.IsNullOrEmpty(userId)) return;

            var state = _state.Get(userId);
            var utcNow = DateTime.UtcNow;
            var currentTicks = e.Session?.PlayState?.PositionTicks ?? 0;

            // 1. Track Time (Budget)
            // Only add minutes if we have a previous progress time to diff against, or assume 1 min intervals?
            // The original code just added 1 minute every time this fired. 
            // PlaybackProgress usually fires every ~10s or so. Adding 1 minute every event is WRONG if it fires often.
            // Better: Track accumulated time.
            if (state.LastProgressUtc.HasValue)
            {
                var elapsed = utcNow - state.LastProgressUtc.Value;
                // Only count if playing
                if (e.Session?.PlayState?.IsPaused == false)
                {
                    // We'll accumulate seconds and convert to minutes in StateService if we want precision, 
                    // but for now, let's stick to the existing "AddMinutes" but throttle it?
                    // Or better: Just check if enough time passed to count as a "minute" or fraction.
                    // The previous code was: _state.AddMinutes(userId, 1, DateTime.UtcNow); 
                    // This implies the event fires once a minute? Unlikely.
                    // Let's assume we want to track actual wall-clock time watched.
                    
                    // For safety/simplicity in this refactor, let's just update the timestamp
                    // and rely on a separate scheduled task or robust diffing for budget.
                    // BUT, to keep it compatible with existing StateService which expects int minutes:
                    // We will just check if > 55 seconds passed since last "minute" add?
                    // No, let's stick to the original logic's intent but fix the frequency issue if possible.
                    // Actually, let's look at the original code: it blindly added 1 minute. 
                    // If PlaybackProgress fires every 10s, the user loses 6 minutes of budget per real minute.
                    // FIX: Only add minute if > 60s elapsed since last update.
                    // We need a "LastBudgetUpdateUtc" in state? 
                    // For now, let's use LastProgressUtc to detect seeks, but we need a separate tracker for budget.
                    // Let's hack it: Only add minute if (UtcNow.Minute != LastProgressUtc.Minute).
                    if (state.LastProgressUtc.Value.Minute != utcNow.Minute)
                    {
                        _state.AddMinutes(userId, 1, utcNow);
                    }
                }
            }

            // 2. Detect Seek
            if (state.LastPositionTicks.HasValue && state.LastProgressUtc.HasValue)
            {
                var timeDiff = (utcNow - state.LastProgressUtc.Value).TotalSeconds;
                var tickDiff = (currentTicks - state.LastPositionTicks.Value);
                var tickDiffSeconds = tickDiff / 10000000.0; // 10k ticks per ms * 1000 = 10M per sec

                // If the position jumped significantly more than the wall-clock time elapsed
                // Threshold: 5 seconds variance?
                if (Math.Abs(tickDiffSeconds - timeDiff) > 5.0) 
                {
                    // It's a seek (or network lag catchup, but >5s is likely seek)
                    // Exclude natural progression (tickDiff ~= timeDiff)
                    
                    // Log seek
                    _state.AddSeek(userId, utcNow);
                    
                    var policy = _policies.GetEffectivePolicy(userId, _plugin.Configuration);
                    var decision = _enforce.ShouldAllowSeek(userId, policy, state, utcNow);
                    
                    if (!decision.Allow)
                    {
                        _logger.LogInformation("ParentGuard: blocking seek for {User} due to {Reason}", userId, decision.Reason);
                        TryStop(e.Session?.Id);
                        if (decision.CooldownMinutes.HasValue)
                        {
                            _state.SetCooldown(userId, utcNow.AddMinutes(decision.CooldownMinutes.Value));
                        }
                    }
                }
            }

            // Update state for next event
            state.LastPositionTicks = currentTicks;
            state.LastProgressUtc = utcNow;
        }

        private void OnPlaybackStopped(object? sender, PlaybackStopEventArgs e)
        {
            var userId = e.Session != null ? e.Session.UserId.ToString() : null;
            if (string.IsNullOrEmpty(userId)) return;

            var state = _state.Get(userId);
            state.LastStopUtc = DateTime.UtcNow;
            state.LastItemId = e.Item?.Id;
            
            // Clear in-progress tracking
            state.LastPositionTicks = null;
            state.LastProgressUtc = null;
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


