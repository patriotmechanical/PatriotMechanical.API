using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PatriotMechanical.API.Infrastructure.Data;

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
            // Wait 30 seconds before first sync to let the app fully start
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            DateTime lastDemoReset = DateTime.MinValue;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _services.CreateScope())
                    {
                        var engine = scope.ServiceProvider
                            .GetRequiredService<ServiceTitanSyncEngine>();

                        await engine.SyncCustomersAsync(fullSync: false);
                        await engine.SyncJobsAsync(fullSync: false);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BackgroundSync] Error: {ex.Message}");
                }

                // Reset demo data once per day
                try
                {
                    if (DateTime.UtcNow - lastDemoReset > TimeSpan.FromHours(24))
                    {
                        using var scope = _services.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        await DemoSeeder.ResetDemoDataAsync(db);
                        lastDemoReset = DateTime.UtcNow;
                        Console.WriteLine("[BackgroundSync] Demo data reset complete.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BackgroundSync] Demo reset error: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
        }
    }
}