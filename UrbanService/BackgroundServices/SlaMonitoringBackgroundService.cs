using Microsoft.Extensions.Options;
using UrbanService.BLL.Common.Helpers;
using UrbanService.BLL.Interfaces;
using UrbanService.BLL.Options;

namespace UrbanService.BackgroundServices;

public sealed class SlaMonitoringBackgroundService
    : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<SlaMonitoringOptions> _optionsMonitor;
    private readonly ILogger<SlaMonitoringBackgroundService> _logger;

    public SlaMonitoringBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<SlaMonitoringOptions> optionsMonitor,
        ILogger<SlaMonitoringBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SLA Monitoring Background Service is starting.");

        try
        {
            await DelayBeforeFirstRunAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var options = _optionsMonitor.CurrentValue;

                if (options.Enabled)
                {
                    await ExecuteMonitoringCycleAsync(stoppingToken);
                }
                else
                {
                    _logger.LogDebug(
                        "SLA Monitoring Background Service is disabled.");
                }

                var intervalMinutes =
                    Math.Max(1, options.IntervalMinutes);

                _logger.LogDebug(
                    "Next SLA monitoring cycle will run after {IntervalMinutes} minute(s).",
                    intervalMinutes);

                await Task.Delay(
                    TimeSpan.FromMinutes(intervalMinutes),
                    stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation(
                "SLA Monitoring Background Service is stopping.");
        }
        catch (Exception exception)
        {
            _logger.LogCritical(
                exception,
                "SLA Monitoring Background Service stopped unexpectedly.");
        }
    }

    private async Task DelayBeforeFirstRunAsync(
        CancellationToken stoppingToken)
    {
        var initialDelaySeconds =
            Math.Max(
                0,
                _optionsMonitor.CurrentValue.InitialDelaySeconds);

        if (initialDelaySeconds == 0)
        {
            return;
        }

        _logger.LogInformation(
            "SLA monitoring will start after {InitialDelaySeconds} second(s).",
            initialDelaySeconds);

        await Task.Delay(
            TimeSpan.FromSeconds(initialDelaySeconds),
            stoppingToken);
    }

    private async Task ExecuteMonitoringCycleAsync(
        CancellationToken stoppingToken)
    {
        var startedAt =
            SlaDateTimeHelper.UtcNow;

        _logger.LogInformation(
            "SLA monitoring cycle started at {StartedAtUtc}.",
            startedAt);

        try
        {
            await using var scope =
                _scopeFactory.CreateAsyncScope();

            var slaService =
                scope.ServiceProvider
                    .GetRequiredService<ISlaService>();

            var updatedCount =
                await slaService.CheckAllRunningSlasAsync();

            var elapsed =
                SlaDateTimeHelper.UtcNow - startedAt;

            _logger.LogInformation(
                "SLA monitoring cycle completed successfully. " +
                "Updated {UpdatedCount} SLA(s) in {ElapsedMilliseconds} ms.",
                updatedCount,
                elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "An error occurred during SLA monitoring cycle.");
        }
    }

    public override Task StopAsync(
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "SLA Monitoring Background Service is being stopped.");

        return base.StopAsync(cancellationToken);
    }
}