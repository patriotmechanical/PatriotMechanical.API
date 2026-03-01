using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PatriotMechanical.API.Application.Services
{
    public class ServiceTitanBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _services;

        public ServiceTitanBackgroundService(IServiceProvider services)
        {
            _services = services;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _services.CreateScope())
                {
                    var engine = scope.ServiceProvider
                        .GetRequiredService<ServiceTitanSyncEngine>();

                    // Incremental mode (one page per run)
                    await engine.SyncCustomersAsync(fullSync: false);
                    await engine.SyncJobsAsync(fullSync: false);
                    // await _syncEngine.SyncInvoicesAsync();
                }

                // Wait 15 minutes
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
        }
    }
}