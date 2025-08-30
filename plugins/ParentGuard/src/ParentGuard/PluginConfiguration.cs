using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ParentGuard
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public Dictionary<string, ProfilePolicy> Profiles { get; set; } = new();
        public Dictionary<string, AdminUser> Admins { get; set; } = new();
        public NotificationsConfig Notifications { get; set; } = new();
    }

    public class ProfilePolicy
    {
        public bool Enabled { get; set; } = true;
        public int DailyBudgetMinutes { get; set; } = 90;
        public Dictionary<string, int> BudgetsByDow { get; set; } = new();
        public Dictionary<string, List<TimeWindow>> Schedules { get; set; } = new();
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

    public class AdminUser
    {
        public bool CanApprove { get; set; } = true;
        public string PinHash { get; set; } = string.Empty; // argon2id planned
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


