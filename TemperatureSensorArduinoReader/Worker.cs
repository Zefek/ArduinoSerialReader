using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TemperatureSensorArduinoReader;
internal class Worker : BackgroundService
{
    private SerialPort serialPort;
    private readonly IOptions<TemperatureAppSettings> options;
    private readonly ILogger<Worker> logger;
    private readonly IServiceProvider serviceProvider;
    private CancellationToken cancellationToken;

    public Worker(IOptions<TemperatureAppSettings> options, ILogger<Worker> logger, IServiceProvider serviceProvider)
    {
        this.options = options;
        this.logger = logger;
        this.serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        cancellationToken = stoppingToken;
        logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

        using var scope = serviceProvider.CreateScope();
        var sensorService = scope.ServiceProvider.GetService<SensorService>();
        await sensorService.SendAllSensorsDiscovery(stoppingToken);
        serialPort = new SerialPort(options.Value.COMPort, 9600);
        serialPort.DataReceived += Sp_DataReceived;
        serialPort.Open();
        while (!stoppingToken.IsCancellationRequested)
        {
            // Keep the service running
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async void Sp_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        logger.LogInformation("Data received at: {time}", DateTimeOffset.Now);
        Thread.Sleep(1000);
        var buffer = new byte[serialPort.BytesToRead];
        serialPort.Read(buffer, 0, serialPort.BytesToRead);
        await ProcessBuffer(buffer, cancellationToken);
    }

    async Task ProcessBuffer(byte[] buffer, CancellationToken cancellationToken)
    {
        var data = new List<byte>();
        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] == 13 && buffer[i + 1] == 10)
            {
                if (data.Count != 5)
                {
                    //Error - musí jich být pět
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (var d in data)
                    {
                        sb.Append(string.Format("{0:X2}", d));
                    }
                    logger.LogInformation("Data received: {data}", sb.ToString());
                    using var scope = serviceProvider.CreateScope();
                    var sensorPipeline = scope.ServiceProvider.GetService<SensorPipeline>();
                    await sensorPipeline.Process(new SensorData { Data = data.ToArray() }, cancellationToken);
                }
                data.Clear();
            }
            else
            {
                data.Add(buffer[i]);
            }
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Worker stopping at: {time}", DateTimeOffset.Now);
        if (serialPort != null && serialPort.IsOpen)
        {
            serialPort.Close();
            serialPort.Dispose();
        }
        return base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        base.Dispose();
        serialPort?.Dispose();
    }
}
