using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Jellyfin.Plugin.ParentGuard.Services
{
    public interface IStateService
    {
        UserState Get(string userId);
        void AddSeek(string userId, DateTime utcNow);
        void AddSwitch(string userId, DateTime utcNow);
        void AddMinutes(string userId, int minutes, DateTime utcNow);
        void SetCooldown(string userId, DateTime untilUtc);
        DateTime? GetCooldown(string userId);
        void SetUnlock(string userId, DateTime untilUtc, string reason);
        DateTime? GetUnlock(string userId);
        void ResetDaily(DateTime utcNow);
    }

    public class StateService : IStateService
    {
        private readonly ConcurrentDictionary<string, UserState> _states = new();

        public UserState Get(string userId) => _states.GetOrAdd(userId, _ => new UserState());

        public void AddSeek(string userId, DateTime utcNow)
        {
            var s = Get(userId);
            s.SeekEvents.Add(utcNow);
        }

        public void AddSwitch(string userId, DateTime utcNow)
        {
            var s = Get(userId);
            s.SwitchEvents.Add(utcNow);
        }

        public void AddMinutes(string userId, int minutes, DateTime utcNow)
        {
            var s = Get(userId);
            if (s.DayKey != utcNow.Date)
            {
                s.DayKey = utcNow.Date;
                s.MinutesConsumed = 0;
                s.SeekEvents.Clear();
                s.SwitchEvents.Clear();
            }
            s.MinutesConsumed += minutes;
        }

        public void SetCooldown(string userId, DateTime untilUtc)
        {
            var s = Get(userId);
            s.CooldownUntilUtc = untilUtc;
        }

        public DateTime? GetCooldown(string userId)
        {
            var s = Get(userId);
            return s.CooldownUntilUtc;
        }

        public void SetUnlock(string userId, DateTime untilUtc, string reason)
        {
            var s = Get(userId);
            s.ActiveUnlockUntilUtc = untilUtc;
            s.ActiveUnlockReason = reason;
        }

        public DateTime? GetUnlock(string userId)
        {
            var s = Get(userId);
            return s.ActiveUnlockUntilUtc;
        }

        public void ResetDaily(DateTime utcNow)
        {
            foreach (var kv in _states)
            {
                kv.Value.DayKey = utcNow.Date;
                kv.Value.MinutesConsumed = 0;
                kv.Value.SeekEvents.Clear();
                kv.Value.SwitchEvents.Clear();
                kv.Value.CooldownUntilUtc = null;
                if (kv.Value.ActiveUnlockUntilUtc.HasValue && kv.Value.ActiveUnlockUntilUtc.Value < utcNow)
                {
                    kv.Value.ActiveUnlockUntilUtc = null;
                    kv.Value.ActiveUnlockReason = string.Empty;
                }
            }
        }
    }

    public class UserState
    {
        public DateTime DayKey { get; set; } = DateTime.UtcNow.Date;
        public int MinutesConsumed { get; set; }
        public List<DateTime> SeekEvents { get; } = new();
        public List<DateTime> SwitchEvents { get; } = new();
        public DateTime? CooldownUntilUtc { get; set; }
        public DateTime? ActiveUnlockUntilUtc { get; set; }
        public string ActiveUnlockReason { get; set; } = string.Empty;

        // Seek detection state
        public long? LastPositionTicks { get; set; }
        public DateTime? LastProgressUtc { get; set; }

        // Switch detection state
        public Guid? LastItemId { get; set; }
        public DateTime? LastStopUtc { get; set; }
    }
}


