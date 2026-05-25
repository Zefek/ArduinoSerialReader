using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace TemperatureSensorArduinoReader.TopicStrategies;

internal class HeaterOutTempStrategy : ITopicStrategy
{
    private readonly SensorPipeline sensorPipeline;
    private readonly ILogger<HeaterOutTempStrategy> logger;

    public HeaterOutTempStrategy(SensorPipeline sensorPipeline, ILogger<HeaterOutTempStrategy> logger)
    {
        this.sensorPipeline = sensorPipeline;
        this.logger = logger;
    }

    public async Task Handle(string topic, byte[] payload, CancellationToken cancellationToken)
    {
        var message = Encoding.UTF8.GetString(payload);
        if (!TryParseSensorFrame(message, out var frame))
        {
            logger.LogWarning("Invalid sensor frame on topic {topic}: {payload}", topic, message);
            return;
        }
        await sensorPipeline.Process(new SensorData { Data = frame }, cancellationToken);
    }

    private static bool TryParseSensorFrame(string payload, out byte[] frame)
    {
        frame = null;
        if (string.IsNullOrEmpty(payload) || payload.Length != 10)
        {
            return false;
        }
        var bytes = new byte[5];
        for (int i = 0; i < 5; i++)
        {
            if (!byte.TryParse(payload.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[i]))
            {
                return false;
            }
        }
        frame = bytes;
        return true;
    }
}
