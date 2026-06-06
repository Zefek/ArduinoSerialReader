using Microsoft.Extensions.Logging;
using System.Buffers;
using TemperatureSensorArduinoReader.Resolvers;

namespace TemperatureSensorArduinoReader.TopicStrategies;

internal class HeaterOutTempStrategy : ITopicStrategy
{
    private const int FrameLength = 5;

    private readonly SensorPipeline sensorPipeline;
    private readonly ILogger<HeaterOutTempStrategy> logger;
    private readonly TX07K_TXC_Resolver resolver;

    public HeaterOutTempStrategy(SensorPipeline sensorPipeline, ILogger<HeaterOutTempStrategy> logger, TX07K_TXC_Resolver resolver)
    {
        this.sensorPipeline = sensorPipeline;
        this.logger = logger;
        this.resolver = resolver;
    }

    public async Task Handle(string topic, ReadOnlySequence<byte> payload, CancellationToken cancellationToken)
    {
        if (payload.Length != FrameLength)
        {
            logger.LogWarning("Invalid sensor frame on topic {topic}: expected {expected} bytes, got {actual}", topic, FrameLength, payload.Length);
            return;
        }
        var sensorData = resolver.Resolve(payload);
        await sensorPipeline.Process(sensorData, cancellationToken);
    }
}
