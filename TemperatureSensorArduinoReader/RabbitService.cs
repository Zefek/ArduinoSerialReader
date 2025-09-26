using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using System.Text;

namespace TemperatureSensorArduinoReader
{
    public class RabbitService : IDisposable
    {
        public readonly IMqttClient managedMqttClientPublisher;
        private readonly MqttClientOptions options;

        public EventHandler HomeAssistantOnline { get; set; }

        public RabbitService(IOptions<TemperatureAppSettings> optionsTemp) 
        {
            var mqttFactory = new MqttFactory();
            var tlsOptions = new MqttClientTlsOptions
            {
                UseTls = true,
                IgnoreCertificateChainErrors = true,
                IgnoreCertificateRevocationErrors = true,
                AllowUntrustedCertificates = true,
                CertificateValidationHandler = (a) => true
            };

            this.options = new MqttClientOptions
            {
                ProtocolVersion = MqttProtocolVersion.V311,
                ChannelOptions = new MqttClientTcpOptions
                {
                    Server = optionsTemp.Value.MqttBroker,
                    Port = optionsTemp.Value.MqttPort,
                    TlsOptions = tlsOptions
                },
                KeepAlivePeriod = TimeSpan.FromSeconds(60)
            };

            options.Credentials = new MqttClientCredentials(optionsTemp.Value.MQTTUsername, Encoding.UTF8.GetBytes(optionsTemp.Value.MQTTPassword));

            options.CleanSession = true;
            options.KeepAlivePeriod = TimeSpan.FromSeconds(5);

            managedMqttClientPublisher = mqttFactory.CreateMqttClient();
            managedMqttClientPublisher.ConnectedAsync += async e =>
            {
                await managedMqttClientPublisher.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("homeassistant/status").Build());
            };
            managedMqttClientPublisher.ApplicationMessageReceivedAsync += e =>
            {
                if (e.ApplicationMessage.Topic == "homeassistant/status" && Encoding.UTF8.GetString(e.ApplicationMessage.Payload) == "online")
                {
                    HomeAssistantOnline?.Invoke(this, EventArgs.Empty);
                }
                return Task.CompletedTask;
            };
            managedMqttClientPublisher.ConnectAsync(options).Wait();

        }

        public async Task Publish(string data, string topic)
        {
            await managedMqttClientPublisher.PublishStringAsync(topic, data);
        }

        public void Dispose()
        {
            managedMqttClientPublisher?.Dispose();
        }
    }
}
