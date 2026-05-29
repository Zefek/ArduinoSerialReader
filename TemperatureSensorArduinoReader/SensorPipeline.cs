using Microsoft.Extensions.Logging;

namespace TemperatureSensorArduinoReader;

internal class SensorPipeline
{
    private readonly SensorRepository sensorRepository;
    private readonly SensorService sensorService;
    private readonly ILogger<SensorPipeline> logger;

    public SensorPipeline(SensorRepository sensorRepository, SensorService sensorService, ILogger<SensorPipeline> logger)
    {
        this.sensorRepository = sensorRepository;
        this.sensorService = sensorService;
        this.logger = logger;
    }

    public async Task Process(SensorData data, CancellationToken cancellationToken)
    {
        try
        {
            var sensor = new Sensor(data);
            var existingSensor = sensorRepository.GetSensor(sensor.Id, sensor.Channel);
            if (existingSensor == null)
            {
                sensorRepository.Add(sensor);
                logger.LogInformation("New sensor added: {sensor}", sensor.Name);
                existingSensor = sensor;
            }
            else
            {
                existingSensor.Update(data);
                logger.LogInformation("Sensor updated: {sensor}", sensor.Name);
            }
            sensorRepository.SaveState(existingSensor);
            sensorRepository.SaveReading(existingSensor);
            await sensorService.PublishSensorData(existingSensor, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing sensor data: {message}", ex.Message);
        }
    }
}
