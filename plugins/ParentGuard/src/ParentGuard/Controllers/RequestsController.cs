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
        public RequestsController(IRequestsStore store)
        {
            _store = store;
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
                // TODO: set unlocks on the target user once we attach requests to users
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
            return Ok(new { userId, status = "unlocked", body });
        }

        [HttpPost("profiles/{userId}/lock")]
        public IActionResult Lock(string userId, [FromBody] LockBody body)
        {
            return Ok(new { userId, status = "locked", body });
        }

        public record ApproveRequest(int? durationMinutes, bool untilEndOfDay = false);
        public record UnlockBody(int durationMinutes, string reason);
        public record LockBody(int cooldownMinutes);
    }
}


