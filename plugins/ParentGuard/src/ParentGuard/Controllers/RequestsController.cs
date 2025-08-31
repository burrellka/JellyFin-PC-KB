using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Jellyfin.Plugin.ParentGuard.Services;

namespace Jellyfin.Plugin.ParentGuard.Controllers
{
    [ApiController]
    [Route("ParentGuard")] 
    [Authorize(Policy = "RequiresElevation")] // admin only
    public class RequestsController : ControllerBase
    {
        private readonly IRequestsStore _store;
        private readonly IStateService _state;
        public RequestsController(IRequestsStore store, IStateService state)
        {
            _store = store;
            _state = state;
        }

        [HttpGet("requests")]
        public IActionResult GetRequests()
        {
            return Ok(new { items = _store.List() });
        }

        [HttpPost("requests/{id}/approve")]
        public IActionResult Approve(string id, [FromBody] ApproveRequest body)
        {
            if (_store.TryApprove(id, body.durationMinutes, body.untilEndOfDay, out var item))
            {
                // Apply unlock if we have an approval duration
                if (item.ApprovalDurationMinutes.HasValue && item.ApprovalDurationMinutes.Value > 0)
                {
                    var now = DateTime.UtcNow;
                    var until = body.untilEndOfDay
                        ? DateTime.UtcNow.Date.AddDays(1) // midnight UTC end of day
                        : now.AddMinutes(item.ApprovalDurationMinutes.Value);
                    _state.SetUnlock(item.UserId, until, "approved_request");
                    // Clear cooldown if any
                    _state.SetCooldown(item.UserId, now.AddSeconds(-1));
                }
                return Ok(item);
            }
            return NotFound();
        }

        [HttpPost("requests/{id}/deny")]
        public IActionResult Deny(string id)
        {
            if (_store.TryDeny(id, out var item))
            {
                return Ok(item);
            }
            return NotFound();
        }

        [HttpPost("profiles/{userId}/unlock")]
        public IActionResult Unlock(string userId, [FromBody] UnlockBody body)
        {
            var now = DateTime.UtcNow;
            var until = now.AddMinutes(body.durationMinutes);
            _state.SetUnlock(userId, until, body.reason);
            // Clear cooldown if any
            _state.SetCooldown(userId, now.AddSeconds(-1));
            return Ok(new { userId, status = "unlocked", untilUtc = until, reason = body.reason });
        }

        [HttpPost("profiles/{userId}/lock")]
        public IActionResult Lock(string userId, [FromBody] LockBody body)
        {
            var now = DateTime.UtcNow;
            var until = now.AddMinutes(body.cooldownMinutes);
            _state.SetCooldown(userId, until);
            return Ok(new { userId, status = "locked", cooldownUntilUtc = until });
        }

        public record ApproveRequest(int? durationMinutes, bool untilEndOfDay = false);
        public record UnlockBody(int durationMinutes, string reason);
        public record LockBody(int cooldownMinutes);
    }
}


