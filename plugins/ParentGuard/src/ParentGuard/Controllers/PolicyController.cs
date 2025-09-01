using System.Collections.Generic;
using System.Linq;
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
        
        // UI DTOs to keep the config XML-safe while exposing dictionary-shaped JSON
        public class UiScheduleWindow { public string Start { get; set; } = "07:00"; public string End { get; set; } = "19:00"; }
        public class UiPolicy
        {
            public bool Enabled { get; set; } = true;
            public int DailyBudgetMinutes { get; set; } = 240;
            public Dictionary<string, int> BudgetsByDow { get; set; } = new();
            public Dictionary<string, List<UiScheduleWindow>> Schedules { get; set; } = new();
            public RateLimit SeekRateLimit { get; set; } = new();
            public RateLimit SwitchRateLimit { get; set; } = new();
            public int CooldownOnTripMinutes { get; set; } = 30;
            public bool PinOverridesAllowed { get; set; } = true;
            public bool PinRequiredForExtraTime { get; set; } = true;
            public int UnlockMaxDurationMinutes { get; set; } = 180;
            public List<string> BlockedCollections { get; set; } = new();
            public string Notes { get; set; } = string.Empty;
        }

        private static UiPolicy ToUiPolicy(ProfilePolicy p)
        {
            var ui = new UiPolicy
            {
                Enabled = p.Enabled,
                DailyBudgetMinutes = p.DailyBudgetMinutes,
                SeekRateLimit = p.SeekRateLimit ?? new RateLimit(),
                SwitchRateLimit = p.SwitchRateLimit ?? new RateLimit(),
                CooldownOnTripMinutes = p.CooldownOnTripMinutes,
                PinOverridesAllowed = p.PinOverridesAllowed,
                PinRequiredForExtraTime = p.PinRequiredForExtraTime,
                UnlockMaxDurationMinutes = p.UnlockMaxDurationMinutes,
                BlockedCollections = p.BlockedCollections ?? new List<string>(),
                Notes = p.Notes ?? string.Empty
            };
            if (p.Budgets != null)
            {
                foreach (var b in p.Budgets)
                {
                    if (!string.IsNullOrEmpty(b.Label)) ui.BudgetsByDow[b.Label] = b.Minutes;
                }
            }
            if (p.Schedules != null)
            {
                foreach (var s in p.Schedules)
                {
                    if (string.IsNullOrEmpty(s.Label)) continue;
                    ui.Schedules[s.Label] = (s.Windows ?? new List<TimeWindow>())
                        .Select(w => new UiScheduleWindow { Start = w.Start, End = w.End }).ToList();
                }
            }
            return ui;
        }

        private static ProfilePolicy FromUiPolicy(UiPolicy ui)
        {
            var p = new ProfilePolicy
            {
                Enabled = ui.Enabled,
                DailyBudgetMinutes = ui.DailyBudgetMinutes,
                SeekRateLimit = ui.SeekRateLimit ?? new RateLimit(),
                SwitchRateLimit = ui.SwitchRateLimit ?? new RateLimit(),
                CooldownOnTripMinutes = ui.CooldownOnTripMinutes,
                PinOverridesAllowed = ui.PinOverridesAllowed,
                PinRequiredForExtraTime = ui.PinRequiredForExtraTime,
                UnlockMaxDurationMinutes = ui.UnlockMaxDurationMinutes,
                BlockedCollections = ui.BlockedCollections ?? new List<string>(),
                Notes = ui.Notes ?? string.Empty,
                Budgets = new List<BudgetByDay>(),
                Schedules = new List<ScheduleByLabel>()
            };
            if (ui.BudgetsByDow != null)
            {
                foreach (var kv in ui.BudgetsByDow)
                {
                    p.Budgets.Add(new BudgetByDay { Label = kv.Key, Minutes = kv.Value });
                }
            }
            if (ui.Schedules != null)
            {
                foreach (var kv in ui.Schedules)
                {
                    p.Schedules.Add(new ScheduleByLabel
                    {
                        Label = kv.Key,
                        Windows = (kv.Value ?? new List<UiScheduleWindow>())
                            .Select(w => new TimeWindow { Start = w.Start, End = w.End }).ToList()
                    });
                }
            }
            return p;
        }
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
            // Project list-based config to dictionary view for UI with dictionary-shaped properties
            var profilesDict = cfg.Profiles.ToDictionary(p => p.UserId, p => ToUiPolicy(p.Policy));
            var result = new
            {
                Profiles = profilesDict,
                Admins = cfg.GetAdminsDictionary(),
                Notifications = cfg.Notifications
            };
            return Ok(result);
        }

        [HttpPut("{userId}")]
        public IActionResult Upsert(string userId, [FromBody] UiPolicy policy)
        {
            if (_plugin == null)
            {
                return Problem("Plugin not initialized");
            }
            var mapped = FromUiPolicy(policy);
            _plugin.Configuration.UpsertProfile(userId, mapped);
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


