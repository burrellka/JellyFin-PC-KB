using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ParentGuard.Services
{
    public class DailyResetService : BackgroundService
    {
        private readonly ILogger<DailyResetService> _logger;
        private readonly IStateService _state;

        public DailyResetService(ILogger<DailyResetService> logger, IStateService state)
        {
            _logger = logger;
            _state = state;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var next = now.Date.AddDays(1); // midnight local
                var delay = next - now;
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                _logger.LogInformation("ParentGuard: running daily reset");
                _state.ResetDaily(DateTime.UtcNow);
            }
        }
    }
}


