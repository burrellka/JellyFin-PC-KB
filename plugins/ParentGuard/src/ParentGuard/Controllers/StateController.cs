using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Jellyfin.Plugin.ParentGuard.Services;

namespace Jellyfin.Plugin.ParentGuard.Controllers
{
    [ApiController]
    [Route("ParentGuard/state")] 
    [Authorize(Policy = "RequiresElevation")] // admin only
    public class StateController : ControllerBase
    {
        private readonly IStateService _state;
        public StateController(IStateService state)
        {
            _state = state;
        }

        [HttpGet("{userId}")]
        public IActionResult GetState(string userId)
        {
            var s = _state.Get(userId);
            return Ok(new
            {
                userId,
                minutesConsumed = s.MinutesConsumed,
                cooldownUntilUtc = s.CooldownUntilUtc,
                unlockUntilUtc = s.ActiveUnlockUntilUtc,
                unlockReason = s.ActiveUnlockReason
            });
        }
    }
}



