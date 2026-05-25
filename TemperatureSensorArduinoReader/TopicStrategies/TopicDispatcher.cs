using Microsoft.Extensions.DependencyInjection;

namespace TemperatureSensorArduinoReader.TopicStrategies;

public class TopicDispatcher
{
    private readonly IServiceProvider serviceProvider;

    public TopicDispatcher(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public async Task Dispatch(string topic, byte[] payload, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var strategy = scope.ServiceProvider.GetKeyedService<ITopicStrategy>(topic);
        if (strategy != null)
        {
            await strategy.Handle(topic, payload, cancellationToken);
        }
    }
}
