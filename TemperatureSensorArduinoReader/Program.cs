using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Serilog;
using Serilog.Sinks.OpenTelemetry;
using TemperatureSensorArduinoReader;
using TemperatureSensorArduinoReader.Resolvers;
using TemperatureSensorArduinoReader.TopicStrategies;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseWindowsService();

    builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

    var appSettings = builder.Configuration.GetSection("TemperatureAppSettings").Get<TemperatureAppSettings>();

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        if (appSettings != null)
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .WriteTo.Console();

            if (!string.IsNullOrWhiteSpace(appSettings.OtlpEndpoint))
            {
                configuration.WriteTo.OpenTelemetry(o =>
                {
                    o.Endpoint = appSettings.OtlpEndpoint;
                    o.Protocol = OtlpProtocol.Grpc;
                    o.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = "TemperatureSensorArduinoReader"
                    };
                });
            }
        }
    });

    if (!string.IsNullOrWhiteSpace(appSettings?.HealthUrl))
    {
        builder.WebHost.UseUrls(appSettings.HealthUrl);
    }

    builder.Services.Configure<TemperatureAppSettings>(builder.Configuration.GetSection("TemperatureAppSettings"));

    builder.Services.AddMetrics();
    builder.Services.AddSingleton<SensorMetrics>();

    if (!string.IsNullOrWhiteSpace(appSettings?.OtlpEndpoint))
    {
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("TemperatureSensorArduinoReader"))
            .WithMetrics(m => m
                .AddMeter(SensorMetrics.MeterName)
                .AddMeter("Npgsql")
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(appSettings.OtlpEndpoint)));
    }

    var connectionString = builder.Configuration.GetSection("TemperatureAppSettings").GetValue<string>("ConnectionString");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));

    builder.Services.AddScoped<RoomRepository>();
    builder.Services.AddSingleton<RabbitService>();
    builder.Services.AddScoped<SensorService>();
    builder.Services.AddScoped<SensorRepository>();
    builder.Services.AddScoped<SensorPipeline>();
    builder.Services.AddSingleton<TopicDispatcher>();
    builder.Services.AddSingleton<TX07KTXCResolver>();
    builder.Services.AddSingleton<GarageResolver>();
    builder.Services.AddKeyedScoped<ITopicStrategy, HomeAssistantOnlineStrategy>(MqttTopics.HomeAssistantStatus);
    builder.Services.AddKeyedScoped<ITopicStrategy, HeaterOutTempStrategy>(MqttTopics.HeaterOutTemp);
    builder.Services.AddKeyedScoped<ITopicStrategy, GarageTemperatureStrategy>(MqttTopics.GarageTemperature);
    builder.Services.AddHostedService<Worker>();
    builder.Services.AddScoped<RoomService>();
    builder.Services.AddHostedService<HomeAssistantService>();

    builder.Services.AddHealthChecks()
        .AddNpgSql(
            connectionString: connectionString!,
            name: "postgres-asr",
            tags: new[] { "ready" });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
    }

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = WriteHealthResponse
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = r => r.Tags.Contains("ready"),
        ResponseWriter = WriteHealthResponse
    });

    app.Run();
}
catch (Exception ex)
{
    await File.WriteAllTextAsync("startup_error.txt", ex.ToString());
    throw;
}

static Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    var payload = new
    {
        status = report.Status.ToString(),
        totalDuration = report.TotalDuration.ToString(),
        entries = report.Entries.ToDictionary(
            e => e.Key,
            e => new
            {
                data = e.Value.Data,
                description = e.Value.Description,
                duration = e.Value.Duration.ToString(),
                status = e.Value.Status.ToString(),
                tags = e.Value.Tags
            })
    };
    return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
}
