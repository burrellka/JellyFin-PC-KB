using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ParentGuard
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        // XML-serializable collections (avoid IDictionary)
        public List<ProfileEntry> Profiles { get; set; } = new();
        public List<AdminEntry> Admins { get; set; } = new();
        public NotificationsConfig Notifications { get; set; } = new();

        // Helpers for controllers/services expecting dictionary views
        public Dictionary<string, ProfilePolicy> GetProfilesDictionary()
        {
            return Profiles.ToDictionary(p => p.UserId, p => p.Policy);
        }

        public Dictionary<string, AdminUser> GetAdminsDictionary()
        {
            return Admins.ToDictionary(a => a.UserId, a => a.Admin);
        }

        public void UpsertProfile(string userId, ProfilePolicy policy)
        {
            var existing = Profiles.FirstOrDefault(p => p.UserId == userId);
            if (existing != null)
            {
                existing.Policy = policy;
            }
            else
            {
                Profiles.Add(new ProfileEntry { UserId = userId, Policy = policy });
            }
        }

        public void RemoveProfile(string userId)
        {
            Profiles.RemoveAll(p => p.UserId == userId);
        }

        public void SetAdminsFromDictionary(Dictionary<string, AdminUser> admins)
        {
            Admins = admins.Select(kv => new AdminEntry { UserId = kv.Key, Admin = kv.Value }).ToList();
        }

        public void SetAdmin(string userId, AdminUser admin)
        {
            var existing = Admins.FirstOrDefault(a => a.UserId == userId);
            if (existing != null)
            {
                existing.Admin = admin;
            }
            else
            {
                Admins.Add(new AdminEntry { UserId = userId, Admin = admin });
            }
        }

        public void RemoveAdmin(string userId)
        {
            Admins.RemoveAll(a => a.UserId == userId);
        }
    }

    public class ProfilePolicy
    {
        public bool Enabled { get; set; } = true;
        public int DailyBudgetMinutes { get; set; } = 90;
        // XML-serializable structures (no IDictionary)
        public List<BudgetByDay> Budgets { get; set; } = new();
        public List<ScheduleByLabel> Schedules { get; set; } = new();
        public RateLimit SeekRateLimit { get; set; } = new();
        public RateLimit SwitchRateLimit { get; set; } = new();
        public int CooldownOnTripMinutes { get; set; } = 5;
        public bool PinOverridesAllowed { get; set; } = true;
        public bool PinRequiredForExtraTime { get; set; } = true;
        public int UnlockMaxDurationMinutes { get; set; } = 180;
        public List<string> BlockedCollections { get; set; } = new();
        public string Notes { get; set; } = string.Empty;
    }

    public class TimeWindow
    {
        public string Start { get; set; } = "16:00";
        public string End { get; set; } = "20:00";
    }

    public class RateLimit
    {
        public int MaxEvents { get; set; } = 6;
        public int WindowMinutes { get; set; } = 10;
    }

    public class BudgetByDay
    {
        public string Label { get; set; } = "Mon"; // Mon, Tue, ..., Mon-Fri, Sat-Sun
        public int Minutes { get; set; } = 240;
    }

    public class ScheduleByLabel
    {
        public string Label { get; set; } = "Mon-Fri"; // Mon, Tue, ..., Mon-Fri, Sat-Sun
        public List<TimeWindow> Windows { get; set; } = new();
    }

    public class AdminUser
    {
        public bool CanApprove { get; set; } = true;
        public string PinHash { get; set; } = string.Empty; // argon2id planned
    }

    // XML-serializable entries
    public class ProfileEntry
    {
        public string UserId { get; set; } = string.Empty;
        public ProfilePolicy Policy { get; set; } = new ProfilePolicy();
    }

    public class AdminEntry
    {
        public string UserId { get; set; } = string.Empty;
        public AdminUser Admin { get; set; } = new AdminUser();
    }

    public class NotificationsConfig
    {
        public string WebhookUrl { get; set; } = string.Empty;
        public EmailConfig Email { get; set; } = new();
    }

    public class EmailConfig
    {
        public bool Enabled { get; set; }
        public string To { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
    }
}


