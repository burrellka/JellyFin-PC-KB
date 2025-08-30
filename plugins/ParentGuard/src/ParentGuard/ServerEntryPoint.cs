// Temporarily disabled until correct Jellyfin 10.10 entrypoint types are confirmed
#if false
using System;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.ParentGuard.Services;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.ParentGuard
{
    public class ServerEntryPoint : IServerEntryPoint
    {
        private readonly ILogger<ServerEntryPoint> _logger;
        private readonly ISessionManager _sessionManager;
        private readonly IPlaybackManager _playbackManager;
        private readonly IPolicyService _policyService;
        private readonly IStateService _stateService;
        private readonly IEnforcementService _enforcementService;
        private readonly Plugin _plugin;

        public ServerEntryPoint(
            ILogger<ServerEntryPoint> logger,
            ISessionManager sessionManager,
            IPlaybackManager playbackManager,
            IPolicyService policyService,
            IStateService stateService,
            IEnforcementService enforcementService,
            Plugin plugin)
        {
            _logger = logger;
            _sessionManager = sessionManager;
            _playbackManager = playbackManager;
            _policyService = policyService;
            _stateService = stateService;
            _enforcementService = enforcementService;
            _plugin = plugin;
        }

        public Task RunAsync()
        {
            _logger.LogInformation("ParentGuard entrypoint starting");

            _sessionManager.SessionStarted += OnSessionStarted;
            _sessionManager.SessionEnded += OnSessionEnded;

            _playbackManager.PlaybackStart += OnPlaybackStart;
            _playbackManager.PlaybackProgress += OnPlaybackProgress;
            _playbackManager.PlaybackStopped += OnPlaybackStopped;

            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _logger.LogInformation("ParentGuard entrypoint stopping");

            _sessionManager.SessionStarted -= OnSessionStarted;
            _sessionManager.SessionEnded -= OnSessionEnded;

            _playbackManager.PlaybackStart -= OnPlaybackStart;
            _playbackManager.PlaybackProgress -= OnPlaybackProgress;
            _playbackManager.PlaybackStopped -= OnPlaybackStopped;

            return Task.CompletedTask;
        }

        private void OnSessionStarted(object? sender, SessionEventArgs e)
        {
            _logger.LogDebug("Session started: {SessionId} {UserId}", e.Session.Id, e.Session.UserId);
        }

        private void OnSessionEnded(object? sender, SessionEventArgs e)
        {
            _logger.LogDebug("Session ended: {SessionId} {UserId}", e.Session.Id, e.Session.UserId);
        }

        private void OnPlaybackStart(object? sender, PlaybackProgressEventArgs e)
        {
            _logger.LogDebug("Playback start: {UserId} {ItemId}", e.UserId, e.ItemId);
            var userId = e.UserId?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(userId)) return;

            var policy = _policyService.GetEffectivePolicy(userId, _plugin.Configuration);
            var state = _stateService.Get(userId);
            var utcNow = DateTime.UtcNow;
            var localNow = DateTime.Now;
            var budget = _policyService.GetDailyBudget(policy, localNow.DayOfWeek);
            if (!_policyService.IsWithinSchedule(policy, localNow))
            {
                _logger.LogInformation("ParentGuard: outside schedule - would block start for {UserId}", userId);
                return; // TODO: actively stop via session
            }
            var decision = _enforcementService.ShouldAllowPlaybackStart(userId, policy, state, utcNow, localNow, budget);
            if (!decision.Allow)
            {
                _logger.LogWarning("ParentGuard: blocking playback start for {UserId} due to {Reason}", userId, decision.Reason);
            }
        }

        private void OnPlaybackProgress(object? sender, PlaybackProgressEventArgs e)
        {
            var userId = e.UserId?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(userId)) return;
            var state = _stateService.Get(userId);
            // For now, add 1 minute every time we get a progress event with 60s elapsed (placeholder).
            _stateService.AddMinutes(userId, 1, DateTime.UtcNow);
        }

        private void OnPlaybackStopped(object? sender, PlaybackStopEventArgs e)
        {
            _logger.LogDebug("Playback stopped: {UserId} {ItemId}", e.UserId, e.ItemId);
        }
    }
}
#endif


