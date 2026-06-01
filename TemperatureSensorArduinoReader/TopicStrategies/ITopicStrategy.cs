using System.Buffers;

namespace TemperatureSensorArduinoReader.TopicStrategies;

internal interface ITopicStrategy
{
    Task Handle(string topic, ReadOnlySequence<byte> payload, CancellationToken cancellationToken);
}
