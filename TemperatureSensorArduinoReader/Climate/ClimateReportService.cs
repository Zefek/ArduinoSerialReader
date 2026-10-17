using System.Globalization;

namespace TemperatureSensorArduinoReader.Climate;

class ClimateReportService : BackgroundService
{
    readonly IServiceScopeFactory scopeFactory;
    readonly ClimateSettings settings;
    readonly ILogger<ClimateReportService> logger;

    public ClimateReportService(IServiceScopeFactory scopeFactory, ClimateSettings settings, ILogger<ClimateReportService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.settings = settings;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var runAt = TimeOnly.Parse(settings.Options.RunAt, CultureInfo.InvariantCulture);
        if (settings.Options.RunOnStartup)
        {
            await RunYesterday(stoppingToken);
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var next = now.Date + runAt.ToTimeSpan();
            if (next <= now)
            {
                next = next.AddDays(1);
            }
            logger.LogInformation("Next climate report at {Next}", next);
            try
            {
                await Task.Delay(next - now, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            await RunYesterday(stoppingToken);
        }
    }

    async Task RunYesterday(CancellationToken stoppingToken)
    {
        var yesterday = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        using var scope = scopeFactory.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<ClimateReportRunner>();
        await runner.Run([yesterday], new ClimateRunOptions(false, true, null), stoppingToken);
    }
}
