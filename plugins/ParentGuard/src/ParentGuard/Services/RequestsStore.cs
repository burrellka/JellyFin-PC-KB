using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Jellyfin.Plugin.ParentGuard.Services
{
    public interface IRequestsStore
    {
        IEnumerable<RequestItem> List();
        RequestItem Add(string userId, string reason);
        bool TryApprove(string id, int? durationMinutes, bool untilEndOfDay, out RequestItem item);
        bool TryDeny(string id, out RequestItem item);
    }

    public class RequestsStore : IRequestsStore
    {
        private readonly ConcurrentDictionary<string, RequestItem> _items = new();

        public IEnumerable<RequestItem> List() => _items.Values;

        public RequestItem Add(string userId, string reason)
        {
            var item = new RequestItem
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = userId,
                Reason = reason,
                CreatedUtc = DateTime.UtcNow,
                Status = "pending"
            };
            _items[item.Id] = item;
            return item;
        }

        public bool TryApprove(string id, int? durationMinutes, bool untilEndOfDay, out RequestItem item)
        {
            if (_items.TryGetValue(id, out item!))
            {
                item.Status = "approved";
                item.ResponseUtc = DateTime.UtcNow;
                item.ApprovalDurationMinutes = durationMinutes;
                item.UntilEndOfDay = untilEndOfDay;
                return true;
            }
            return false;
        }

        public bool TryDeny(string id, out RequestItem item)
        {
            if (_items.TryGetValue(id, out item!))
            {
                item.Status = "denied";
                item.ResponseUtc = DateTime.UtcNow;
                return true;
            }
            return false;
        }
    }

    public class RequestItem
    {
        public string Id { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // pending/approved/denied
        public DateTime CreatedUtc { get; set; }
        public DateTime? ResponseUtc { get; set; }
        public int? ApprovalDurationMinutes { get; set; }
        public bool UntilEndOfDay { get; set; }
    }
}


