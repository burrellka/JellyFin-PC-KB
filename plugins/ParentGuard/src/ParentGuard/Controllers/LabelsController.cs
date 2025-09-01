using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ParentGuard.Controllers
{
    [ApiController]
    [Route("ParentGuard/labels")] 
    [Authorize(Policy = "RequiresElevation")] // admin only
    public class LabelsController : ControllerBase
    {
        private readonly Plugin _plugin;
        public LabelsController(Plugin plugin)
        {
            _plugin = plugin;
        }

        public LabelsController()
        {
            _plugin = Plugin.Instance!;
        }

        public record LabelBody(string userId, bool parentApprover, bool childProfile);

        [HttpPost]
        public IActionResult Set([FromBody] LabelBody body)
        {
            if (body.parentApprover)
            {
                _plugin.Configuration.Admins[body.userId] = new AdminUser { CanApprove = true };
            }
            else
            {
                _plugin.Configuration.Admins.Remove(body.userId);
            }

            if (body.childProfile)
            {
                _plugin.Configuration.Profiles[body.userId] = _plugin.Configuration.Profiles.ContainsKey(body.userId)
                    ? _plugin.Configuration.Profiles[body.userId]
                    : Defaults.CreateDefaultPolicy();
            }
            else
            {
                _plugin.Configuration.Profiles.Remove(body.userId);
            }

            _plugin.Save();
            return Ok(new { ok = true });
        }
    }
}


