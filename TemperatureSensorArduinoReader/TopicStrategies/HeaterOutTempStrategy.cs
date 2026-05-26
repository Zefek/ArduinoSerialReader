using Microsoft.Extensions.Logging;

namespace TemperatureSensorArduinoReader.TopicStrategies;

internal class HeaterOutTempStrategy : ITopicStrategy
{
    private const int FrameLength = 5;

    private readonly SensorPipeline sensorPipeline;
    private readonly ILogger<HeaterOutTempStrategy> logger;

    public HeaterOutTempStrategy(SensorPipeline sensorPipeline, ILogger<HeaterOutTempStrategy> logger)
    {
        this.sensorPipeline = sensorPipeline;
        this.logger = logger;
    }

    public async Task Handle(string topic, byte[] payload, CancellationToken cancellationToken)
    {
        if (payload == null || payload.Length != FrameLength)
        {
            logger.LogWarning("Invalid sensor frame on topic {topic}: expected {expected} bytes, got {actual}", topic, FrameLength, payload?.Length ?? 0);
            return;
        }
        await sensorPipeline.Process(new SensorData { Data = payload }, cancellationToken);
    }
}
