using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Bot;

/// <summary>
/// Background service for polling Telegram Bot API for updates.
/// </summary>
public class PollingService : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<PollingService> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PollingService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="logger">Logger instance for tracking operations.</param>
    public PollingService(IServiceProvider serviceProvider, ILogger<PollingService> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    /// <summary>
    /// Executes the polling service as a background task.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop the service.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Polling service is starting");

        try
        {
            await RunPollingLoopAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Polling service encountered a critical error and is stopping");
        }
        finally
        {
            logger.LogInformation("Polling service has stopped");
        }
    }

    private async Task RunPollingLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPollingIterationAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                await HandlePollingErrorAsync(ex, stoppingToken);
            }
        }
    }

    private async Task ProcessPollingIterationAsync(CancellationToken stoppingToken)
    {
        // Create a new scope for each iteration to ensure fresh service instances
        using var scope = serviceProvider.CreateScope();
        var receiver = scope.ServiceProvider.GetRequiredService<IReceiverService>();

        logger.LogDebug("Starting polling iteration");

        await receiver.ReceiveAsync(stoppingToken);

        logger.LogDebug("Completed polling iteration successfully");
    }

    private async Task HandlePollingErrorAsync(Exception ex, CancellationToken stoppingToken)
    {
        logger.LogError(ex, "Polling iteration failed with exception");

        // Handle specific Telegram API exceptions
        switch (ex)
        {
            case ApiRequestException apiEx:
                logger.LogWarning("Telegram API request failed: {ErrorCode} - {Message}", apiEx.ErrorCode, apiEx.Message);
                break;

            case RequestException reqEx:
                logger.LogWarning("Telegram request failed, possible network issue: {Message}", reqEx.Message);
                break;
        }

        // Cooldown before retrying after errors
        logger.LogInformation("Waiting 5 seconds before retrying after error");

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
        catch (TaskCanceledException)
        {
            logger.LogDebug("Cooldown delay was cancelled");
        }
    }

    /// <summary>
    /// Triggered when the application host is performing a graceful shutdown.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token indicating that shutdown should be no longer graceful.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Polling service is stopping gracefully");

        await base.StopAsync(cancellationToken);

        logger.LogInformation("Polling service stopped gracefully");
    }
}