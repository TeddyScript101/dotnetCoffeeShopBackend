using CoffeeShopApi.Data;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Services;

public class DbMigrationService(IServiceProvider services, ILogger<DbMigrationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        const int maxAttempts = 6;
        const int retryDelayMs = 8_000;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
                await db.Database.MigrateAsync(cancellationToken);
                await RoleSeeder.SeedRolesAsync(scope.ServiceProvider);
                await DatabaseSeeder.SeedDataAsync(scope.ServiceProvider);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex,
                    "DB init attempt {Attempt}/{Max} failed (Neon cold start?). Retrying in {Delay} ms...",
                    attempt, maxAttempts, retryDelayMs);
                await Task.Delay(retryDelayMs, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex,
                    "DB init failed after {Max} attempts. App is running but DB-dependent endpoints will fail.",
                    maxAttempts);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
