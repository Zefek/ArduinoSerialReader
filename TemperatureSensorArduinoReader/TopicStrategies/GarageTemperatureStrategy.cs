using System.Buffers;
using TemperatureSensorArduinoReader.Resolvers;

namespace TemperatureSensorArduinoReader.TopicStrategies;

internal class GarageTemperatureStrategy : ITopicStrategy
{
    private readonly SensorPipeline sensorPipeline;
    private readonly GarageResolver resolver;

    public GarageTemperatureStrategy(SensorPipeline sensorPipeline, GarageResolver resolver)
    {
        this.sensorPipeline = sensorPipeline;
        this.resolver = resolver;
    }

    public async Task Handle(string topic, ReadOnlySequence<byte> payload, CancellationToken cancellationToken)
    {
        var sensorData = resolver.Resolve(payload);
        await sensorPipeline.Process(sensorData, cancellationToken);
    }
}
