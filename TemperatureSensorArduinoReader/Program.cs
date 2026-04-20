using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using TemperatureSensorArduinoReader;

try
{
    Host.CreateDefaultBuilder(args)
        .UseWindowsService()
        .ConfigureAppConfiguration((hostContext, config) =>
        {
            config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        })
        .UseSerilog((context, services, configuration) =>
        {
            var settings = context.Configuration.GetSection("TemperatureAppSettings").Get<TemperatureAppSettings>();
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .WriteTo.Console()
                .WriteTo.GrafanaLoki(settings.LokiUrl, labels: new[]
                {
                    new LokiLabel { Key = "app", Value = "TemperatureSensorArduinoReader" }
                });
        })
        .ConfigureServices((hostContext, services) =>
        {
            services.Configure<TemperatureAppSettings>(hostContext.Configuration.GetSection("TemperatureAppSettings"));

            var connectionString = hostContext.Configuration.GetSection("TemperatureAppSettings").GetValue<string>("ConnectionString");
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString), ServiceLifetime.Singleton);

            services.AddSingleton<RoomRepository>();
            services.AddSingleton<RabbitService>();
            services.AddSingleton<SensorService>();
            services.AddSingleton<SensorRepository>();
            services.AddHostedService<Worker>();
            services.AddHostedService<HomeAssistantService>();
        })
        .Build()
        .MigrateAndRun();
}
catch (Exception ex)
{
    File.WriteAllText("startup_error.txt", ex.ToString());
    throw;
}

public static class HostExtensions
{
    public static void MigrateAndRun(this IHost host)
    {
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }
        host.Run();
    }
}
