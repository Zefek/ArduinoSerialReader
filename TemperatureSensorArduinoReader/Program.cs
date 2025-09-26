// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging; // Added this using directive for logging extensions
using System.IO.Ports;
using TemperatureSensorArduinoReader;

try
{
    Host.CreateDefaultBuilder(args)
        .UseWindowsService()
        .ConfigureAppConfiguration((hostContext, config) =>
        {
            config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        })
        .ConfigureServices((hostContext, services) =>
        {
            services.Configure<TemperatureAppSettings>(hostContext.Configuration.GetSection("TemperatureAppSettings"));

            services.AddSingleton<RoomRepository>();
            services.AddSingleton<RabbitService>();
            services.AddSingleton<SensorService>();
            services.AddHostedService<Worker>();
            services.AddHostedService<HomeAssistantService>();
        })
        .ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddEventLog(s =>
            {
                s.LogName = "Application";
                s.SourceName = "TemperatureSensorArduinoReader";
            });
            if (Environment.UserInteractive)
            {
                logging.AddConsole();
            }
        })
        .Build()
        .Run();
}
catch (Exception ex)
{
    File.WriteAllText("startup_error.txt", ex.ToString()); // Save errors during startup
    throw;
}