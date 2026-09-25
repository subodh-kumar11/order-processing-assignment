namespace OrderProcessing;

public sealed class ProcessingWorker(OrderStore store, IConfiguration configuration,
    ILogger<ProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = configuration.GetValue("Orders:ProcessingIntervalSeconds", 300);
        if (seconds <= 0) throw new InvalidOperationException("Processing interval must be positive.");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try { logger.LogInformation("Moved {Count} orders to PROCESSING", store.ProcessPending()); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    logger.LogError(error, "Could not persist processing update; will retry on next tick");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
