using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.ParentGuard.Services
{
    public interface IPolicyService
    {
        ProfilePolicy GetEffectivePolicy(string userId, PluginConfiguration config);
        bool IsWithinSchedule(ProfilePolicy policy, DateTime nowLocal);
        int GetDailyBudget(ProfilePolicy policy, DayOfWeek dow);
    }

    public class PolicyService : IPolicyService
    {
        public ProfilePolicy GetEffectivePolicy(string userId, PluginConfiguration config)
        {
            var dict = config.GetProfilesDictionary();
            if (dict.TryGetValue(userId, out var policy))
            {
                return policy;
            }
            return new ProfilePolicy();
        }

        public bool IsWithinSchedule(ProfilePolicy policy, DateTime nowLocal)
        {
            var label = nowLocal.DayOfWeek switch
            {
                DayOfWeek.Monday => "Mon",
                DayOfWeek.Tuesday => "Tue",
                DayOfWeek.Wednesday => "Wed",
                DayOfWeek.Thursday => "Thu",
                DayOfWeek.Friday => "Fri",
                DayOfWeek.Saturday => "Sat",
                DayOfWeek.Sunday => "Sun",
                _ => "Mon"
            };

            foreach (var kv in policy.Schedules)
            {
                var key = kv.Key;
                if (key == label || (key == "Mon-Fri" && nowLocal.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday) || (key == "Sat-Sun" && (nowLocal.DayOfWeek == DayOfWeek.Saturday || nowLocal.DayOfWeek == DayOfWeek.Sunday)))
                {
                    foreach (var window in kv.Value)
                    {
                        if (TimeOnly.TryParse(window.Start, out var s) && TimeOnly.TryParse(window.End, out var e))
                        {
                            var nowT = TimeOnly.FromDateTime(nowLocal);
                            if (nowT >= s && nowT <= e)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return true; // default allow if no schedules
        }

        public int GetDailyBudget(ProfilePolicy policy, DayOfWeek dow)
        {
            var label = dow switch
            {
                DayOfWeek.Monday => "Mon",
                DayOfWeek.Tuesday => "Tue",
                DayOfWeek.Wednesday => "Wed",
                DayOfWeek.Thursday => "Thu",
                DayOfWeek.Friday => "Fri",
                DayOfWeek.Saturday => "Sat",
                DayOfWeek.Sunday => "Sun",
                _ => "Mon"
            };
            if (policy.BudgetsByDow.TryGetValue(label, out var m))
            {
                return m;
            }
            return policy.DailyBudgetMinutes;
        }
    }
}


