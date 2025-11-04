using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Formatter;
using System;
using System.Text;

namespace TemperatureSensorArduinoReader
{
    public class RabbitService : IDisposable
    {
        private const int baseDelayMs = 1000;
        private const int maxDelayMs = 30000;
        public IMqttClient managedMqttClientPublisher;
        private readonly MqttClientOptions options;
        private bool connected = false;
        private int attempt = 0;
        private static readonly Random random = new();
        private readonly ILogger<RabbitService> logger;

        public EventHandler HomeAssistantOnline { get; set; }

        public RabbitService(IOptions<TemperatureAppSettings> optionsTemp, ILogger<RabbitService> logger)
        {
            this.logger = logger;
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
                KeepAlivePeriod = TimeSpan.FromSeconds(60),
                CleanSession = true,
                Credentials = new MqttClientCredentials(optionsTemp.Value.MQTTUsername, Encoding.UTF8.GetBytes(optionsTemp.Value.MQTTPassword))
            };

            Connect();

        }

        private void Connect()
        {
            logger.LogInformation("Connecting to MQTT broker...");
            var mqttFactory = new MqttFactory();
            managedMqttClientPublisher = mqttFactory.CreateMqttClient();
            managedMqttClientPublisher.ConnectedAsync += async e =>
            {
                logger.LogInformation("Connected to MQTT broker.");
                connected = true;
                attempt = 0;
                await managedMqttClientPublisher.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("homeassistant/status").Build());
            };
            managedMqttClientPublisher.ApplicationMessageReceivedAsync += e =>
            {
                logger.LogInformation("Received MQTT message on topic {topic}", e.ApplicationMessage.Topic);
                if (e.ApplicationMessage.Topic == "homeassistant/status" && Encoding.UTF8.GetString(e.ApplicationMessage.Payload) == "online")
                {
                    HomeAssistantOnline?.Invoke(this, EventArgs.Empty);
                }
                return Task.CompletedTask;
            };
            managedMqttClientPublisher.DisconnectedAsync += async e =>
            {
                connected = false;
                logger.LogWarning("Disconnected from MQTT broker.");
                while (!connected)
                {
                    attempt++;
                    int delay = baseDelayMs * (int)Math.Pow(2, attempt);
                    delay = Math.Min(delay, maxDelayMs);
                    int jitter = random.Next(0, delay / 2);
                    await Task.Delay(delay + jitter);
                    try
                    {
                        logger.LogInformation("Reconnecting to MQTT broker, attempt {attempt}...", attempt);
                        await managedMqttClientPublisher.ConnectAsync(options);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error reconnecting to MQTT broker.");
                    }
                }
            };
            try
            {
                managedMqttClientPublisher.ConnectAsync(options).Wait();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error connecting to MQTT broker.");
            }
        }

        public async Task Publish(string data, string topic)
        {
            if (!connected)
            {
                Connect();
            }
            try
            {
                await managedMqttClientPublisher.PublishStringAsync(topic, data);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publishing to MQTT broker.");
                throw;
            }
        }

        public void Dispose()
        {
            managedMqttClientPublisher?.DisconnectAsync().Wait();
            managedMqttClientPublisher?.Dispose();
            managedMqttClientPublisher = null;
        }
    }
}
