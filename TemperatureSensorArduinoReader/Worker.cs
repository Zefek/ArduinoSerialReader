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
    private readonly SensorService sensorService;

    public Worker(IOptions<TemperatureAppSettings> options, ILogger<Worker> logger, SensorService sensorService)
    {
        this.options=options;
        this.logger=logger;
        this.sensorService=sensorService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
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
        await ProcessBuffer(buffer);
    }

    async Task ProcessBuffer(byte[] buffer)
    {
        var data = new List<byte>();
        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] == 13 && buffer[i+1] == 10)
            {
                if (data.Count != 5)
                {
                    //Error - musí jich být pět
                }
                else
                {
                    try
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (var d in data)
                        {
                            sb.Append(string.Format("{0:X2}", d));
                        }
                        logger.LogInformation("Data received: {data}", sb.ToString());
                        var sensor = new Sensor(new SensorData { Data = data.ToArray() });
                        await sensorService.PublishSensorData(sensor);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error processing sensor data: {message}", ex.Message);
                    }
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
