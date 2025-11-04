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
        private static readonly Random random = new();
        private readonly ILogger<RabbitService> logger;
        private readonly CancellationToken cancellationToken;
        private TimeSpan mqttConnectionTimeout = TimeSpan.Zero;
        private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        public EventHandler HomeAssistantOnline { get; set; }

        public RabbitService(IOptions<TemperatureAppSettings> optionsTemp, ILogger<RabbitService> logger, CancellationToken cancellationToken)
        {
            this.logger = logger;
            this.cancellationToken = cancellationToken;
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

            Connect(cancellationToken).Wait();

        }

        private async Task Connect(CancellationToken cancellationToken)
        {
            logger.LogInformation("Connecting to MQTT broker...");
            var mqttFactory = new MqttFactory();
            managedMqttClientPublisher = mqttFactory.CreateMqttClient();
            managedMqttClientPublisher.ConnectedAsync += Connected;
            managedMqttClientPublisher.ApplicationMessageReceivedAsync += MessageReceived;
            managedMqttClientPublisher.DisconnectedAsync += Disconnected;
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                if (!managedMqttClientPublisher.IsConnected)
                {
                    await managedMqttClientPublisher.ConnectAsync(options, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error connecting to MQTT broker.");
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task Connected(MqttClientConnectedEventArgs e)
        {
            logger.LogInformation("Connected to MQTT broker.");
            connected = true;
            mqttConnectionTimeout = TimeSpan.Zero;
            await managedMqttClientPublisher.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("homeassistant/status").Build(), cancellationToken);
        }

        private async Task Disconnected(MqttClientDisconnectedEventArgs e)
        {
            await semaphore.WaitAsync(cancellationToken);
            connected = false;
            logger.LogWarning("Disconnected from MQTT broker.");
            while (!connected)
            {
                if(cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                mqttConnectionTimeout = TimeSpan.FromMilliseconds(Math.Min(mqttConnectionTimeout.TotalMilliseconds * 2 + random.Next(0, 5000), 300000));
                await Task.Delay((int)mqttConnectionTimeout.TotalMilliseconds, cancellationToken);
                try
                {
                    logger.LogInformation("Reconnecting to MQTT broker...");
                    await managedMqttClientPublisher.ConnectAsync(options, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error reconnecting to MQTT broker.");
                }
            }
            semaphore.Release();
        }

        private Task MessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            logger.LogInformation("Received MQTT message on topic {topic}", e.ApplicationMessage.Topic);
            if (e.ApplicationMessage.Topic == "homeassistant/status" && Encoding.UTF8.GetString(e.ApplicationMessage.Payload) == "online")
            {
                HomeAssistantOnline?.Invoke(this, EventArgs.Empty);
            }
            return Task.CompletedTask;
        }

        public async Task Publish(string data, string topic, CancellationToken cancellationToken)
        {
            if (!connected)
            {
                await Connect(cancellationToken);
            }
            try
            {
                await managedMqttClientPublisher.PublishStringAsync(topic, data, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publishing to MQTT broker.");
                throw;
            }
        }

        public void Dispose()
        {
            managedMqttClientPublisher?.DisconnectAsync(cancellationToken:cancellationToken).Wait();
            managedMqttClientPublisher?.Dispose();
            managedMqttClientPublisher = null;
        }
    }
}
