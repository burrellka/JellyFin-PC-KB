using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ParentGuard.Controllers
{
    [ApiController]
    [Route("ParentGuard/policy")] 
    [Authorize(Policy = "RequiresElevation")] // admin only
    public class PolicyController : ControllerBase
    {
        private readonly Plugin? _plugin;
        public PolicyController(Plugin plugin)
        {
            _plugin = plugin;
        }

        // Fallback when DI is unavailable
        public PolicyController()
        {
            _plugin = Plugin.Instance;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var cfg = _plugin?.Configuration ?? new PluginConfiguration();
            // Project list-based config to dictionary view for UI
            var profilesDict = cfg.Profiles.ToDictionary(p => p.UserId, p => p.Policy);
            // Convert list structures back to dictionaries for UI compatibility
            foreach (var kv in profilesDict)
            {
                var policy = kv.Value;
                // no-op: UI uses endpoints that compute budgets/schedules per day/label via PolicyService
            }
            var result = new
            {
                Profiles = profilesDict,
                Admins = cfg.GetAdminsDictionary(),
                Notifications = cfg.Notifications
            };
            return Ok(result);
        }

        [HttpPut("{userId}")]
        public IActionResult Upsert(string userId, [FromBody] ProfilePolicy policy)
        {
            if (_plugin == null)
            {
                return Problem("Plugin not initialized");
            }
            _plugin.Configuration.UpsertProfile(userId, policy);
            _plugin.Save();
            return Ok(policy);
        }

        [HttpPut("admins")] 
        public IActionResult SetAdmins([FromBody] Dictionary<string, AdminUser> admins)
        {
            if (_plugin == null)
            {
                return Problem("Plugin not initialized");
            }
            _plugin.Configuration.SetAdminsFromDictionary(admins);
            _plugin.Save();
            return Ok(admins);
        }

        public record SeedBody(List<string> admins, List<string> profiles);

        [HttpPost("seed")] 
        public IActionResult Seed([FromBody] SeedBody body)
        {
            if (_plugin == null)
            {
                return Problem("Plugin not initialized");
            }
            foreach (var a in body.admins)
            {
                _plugin.Configuration.SetAdmin(a, new AdminUser { CanApprove = true });
            }
            foreach (var p in body.profiles)
            {
                _plugin.Configuration.UpsertProfile(p, Defaults.CreateDefaultPolicy());
            }
            _plugin.Save();
            return Ok(new { admins = body.admins, profiles = body.profiles });
        }
    }
}


