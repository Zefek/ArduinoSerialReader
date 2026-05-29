using System.Text;

namespace TemperatureSensorArduinoReader.TopicStrategies;

internal class HomeAssistantOnlineStrategy : ITopicStrategy
{
    private readonly SensorService sensorService;

    public HomeAssistantOnlineStrategy(SensorService sensorService)
    {
        this.sensorService = sensorService;
    }

    public async Task Handle(string topic, byte[] payload, CancellationToken cancellationToken)
    {
        if (Encoding.UTF8.GetString(payload) == "online")
        {
            await sensorService.SendAllSensorsDiscovery(cancellationToken);
        }
    }
}
