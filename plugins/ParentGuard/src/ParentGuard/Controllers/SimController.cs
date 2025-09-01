using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Jellyfin.Plugin.ParentGuard.Services;

namespace Jellyfin.Plugin.ParentGuard.Controllers
{
    [ApiController]
    [Route("ParentGuard/sim")] 
    [Authorize(Policy = "RequiresElevation")] // admin only
    public class SimController : ControllerBase
    {
        private readonly IRequestsStore _store;
        private readonly IPolicyService _policies;
        private readonly IStateService _state;
        private readonly IEnforcementService _enforce;
        private readonly Plugin _plugin;

        public SimController(IRequestsStore store, IPolicyService policies, IStateService state, IEnforcementService enforce, Plugin plugin)
        {
            _store = store;
            _policies = policies;
            _state = state;
            _enforce = enforce;
            _plugin = plugin;
        }

        // Fallback constructor when DI isn't available
        public SimController()
        {
            _store = Services.ServiceHub.Requests;
            _policies = Services.ServiceHub.Policies;
            _state = Services.ServiceHub.State;
            _enforce = Services.ServiceHub.Enforcement;
            _plugin = Plugin.Instance!;
        }

        [HttpPost("playback/start/{userId}")]
        public IActionResult Start(string userId)
        {
            var policy = _policies.GetEffectivePolicy(userId, _plugin.Configuration);
            var state = _state.Get(userId);
            var utcNow = DateTime.UtcNow;
            var localNow = DateTime.Now;
            var budget = _policies.GetDailyBudget(policy, localNow.DayOfWeek);
            if (!_policies.IsWithinSchedule(policy, localNow))
            {
                _store.Add(userId, "outside_schedule");
                return Ok(new { allow = false, reason = "outside_schedule" });
            }
            var decision = _enforce.ShouldAllowPlaybackStart(userId, policy, state, utcNow, localNow, budget);
            if (!decision.Allow)
            {
                _store.Add(userId, decision.Reason ?? "blocked");
            }
            return Ok(new { allow = decision.Allow, reason = decision.Reason, cooldown = decision.CooldownMinutes });
        }

        [HttpPost("playback/seek/{userId}")]
        public IActionResult Seek(string userId)
        {
            var policy = _policies.GetEffectivePolicy(userId, _plugin.Configuration);
            var state = _state.Get(userId);
            var now = DateTime.UtcNow;
            _state.AddSeek(userId, now);
            var decision = _enforce.ShouldAllowSeek(userId, policy, state, now);
            if (!decision.Allow && decision.CooldownMinutes.HasValue)
            {
                _state.SetCooldown(userId, now.AddMinutes(decision.CooldownMinutes.Value));
                _store.Add(userId, "seek_rate_limit");
            }
            return Ok(new { allow = decision.Allow, reason = decision.Reason, cooldown = decision.CooldownMinutes });
        }

        [HttpPost("playback/switch/{userId}")]
        public IActionResult Switch(string userId)
        {
            var policy = _policies.GetEffectivePolicy(userId, _plugin.Configuration);
            var state = _state.Get(userId);
            var now = DateTime.UtcNow;
            _state.AddSwitch(userId, now);
            var decision = _enforce.ShouldAllowSwitch(userId, policy, state, now);
            if (!decision.Allow && decision.CooldownMinutes.HasValue)
            {
                _state.SetCooldown(userId, now.AddMinutes(decision.CooldownMinutes.Value));
                _store.Add(userId, "switch_rate_limit");
            }
            return Ok(new { allow = decision.Allow, reason = decision.Reason, cooldown = decision.CooldownMinutes });
        }

        [HttpPost("playback/progress/{userId}/{minutes}")]
        public IActionResult Progress(string userId, int minutes)
        {
            _state.AddMinutes(userId, minutes, DateTime.UtcNow);
            return Ok(new { updated = true });
        }
    }
}


