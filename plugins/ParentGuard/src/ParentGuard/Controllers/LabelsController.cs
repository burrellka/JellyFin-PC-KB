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
                _plugin.Configuration.SetAdmin(body.userId, new AdminUser { CanApprove = true });
            }
            else
            {
                _plugin.Configuration.RemoveAdmin(body.userId);
            }

            if (body.childProfile)
            {
                var dict = _plugin.Configuration.GetProfilesDictionary();
                var policy = dict.ContainsKey(body.userId) ? dict[body.userId] : Defaults.CreateDefaultPolicy();
                _plugin.Configuration.UpsertProfile(body.userId, policy);
            }
            else
            {
                _plugin.Configuration.RemoveProfile(body.userId);
            }

            _plugin.Save();
            return Ok(new { ok = true });
        }
    }
}


