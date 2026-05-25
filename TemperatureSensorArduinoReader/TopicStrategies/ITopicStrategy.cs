namespace TemperatureSensorArduinoReader.TopicStrategies;

internal interface ITopicStrategy
{
    Task Handle(string topic, byte[] payload, CancellationToken cancellationToken);
}
