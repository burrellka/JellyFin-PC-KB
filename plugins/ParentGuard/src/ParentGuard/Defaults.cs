using System.Collections.Generic;

namespace Jellyfin.Plugin.ParentGuard
{
    public static class Defaults
    {
        public static ProfilePolicy CreateDefaultPolicy()
        {
            return new ProfilePolicy
            {
                DailyBudgetMinutes = 240,
                Budgets = new List<BudgetByDay>(),
                Schedules = new List<ScheduleByLabel>
                {
                    new ScheduleByLabel { Label = "Mon-Fri", Windows = new List<TimeWindow> { new TimeWindow { Start = "07:00", End = "19:00" } } },
                    new ScheduleByLabel { Label = "Sat-Sun", Windows = new List<TimeWindow> { new TimeWindow { Start = "07:00", End = "19:00" } } }
                },
                SeekRateLimit = new RateLimit { MaxEvents = 3, WindowMinutes = 30 },
                SwitchRateLimit = new RateLimit { MaxEvents = 2, WindowMinutes = 30 },
                CooldownOnTripMinutes = 30,
                PinOverridesAllowed = true,
                PinRequiredForExtraTime = true,
                UnlockMaxDurationMinutes = 180
            };
        }
    }
}


