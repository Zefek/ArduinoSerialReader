using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using System.Text;

namespace TemperatureSensorArduinoReader
{
    internal class RabbitService : IDisposable
    {
        public readonly IMqttClient managedMqttClientPublisher;
        private readonly MqttClientOptions options;

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
                }
            };

            options.Credentials = new MqttClientCredentials(optionsTemp.Value.MQTTUsername, Encoding.UTF8.GetBytes(optionsTemp.Value.MQTTPassword));

            options.CleanSession = true;
            options.KeepAlivePeriod = TimeSpan.FromSeconds(5);

            managedMqttClientPublisher = mqttFactory.CreateMqttClient();
        }

        public async Task Publish(string data, string topic)
        {
            await managedMqttClientPublisher.ConnectAsync(options);
            await managedMqttClientPublisher.PublishStringAsync(topic, data);
            await managedMqttClientPublisher.DisconnectAsync();
        }

        public void Dispose()
        {
            managedMqttClientPublisher?.Dispose();
        }
    }
}
