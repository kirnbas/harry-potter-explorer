using HarryPotterExplorer.Data;

namespace HarryPotterExplorer.Services;

public sealed class HarryPotterApiOptions
{
    public const string SectionName = "HarryPotterApi";

    public string BaseAddress { get; set; } = "https://hp-api.onrender.com/";

    /// <summary>The upstream is a free Render dyno; a cold start can take the best part of a minute.</summary>
    public int TimeoutSeconds { get; set; } = 100;

    public int RetryCount { get; set; } = 3;

    /// <summary>How often the mirror is refreshed while the app is running.</summary>
    public int RefreshIntervalHours { get; set; } = 6;
}

/// <summary>
/// Prepares the database and then keeps the mirror warm. Deliberately does its work
/// *after* the web host is listening, so a sleeping upstream API delays data, never
/// startup - the site renders immediately with an honest "owls are still flying" state.
/// </summary>
public sealed class CatalogSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    ICatalogSyncCoordinator coordinator,
    IConfiguration configuration,
    ILogger<CatalogSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = configuration.GetSection(HarryPotterApiOptions.SectionName)
                          .Get<HarryPotterApiOptions>() ?? new HarryPotterApiOptions();

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HogwartsContext>();
            var initialiser = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            await initialiser.InitialiseAsync(db, stoppingToken);
        }

        var interval = TimeSpan.FromHours(Math.Max(1, options.RefreshIntervalHours));
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await coordinator.SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled mirror refresh threw; will try again in {Interval}", interval);
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
