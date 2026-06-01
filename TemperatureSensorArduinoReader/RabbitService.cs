using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Formatter;
using System;
using System.Text;
using TemperatureSensorArduinoReader.TopicStrategies;

namespace TemperatureSensorArduinoReader
{
    public class RabbitService : IDisposable
    {
        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private IMqttClient? managedMqttClientPublisher;
        private static readonly Random random = new();
        private readonly IOptions<TemperatureAppSettings> temperatureAppSettings;
        private readonly ILogger<RabbitService> logger;
        private readonly TopicDispatcher topicDispatcher;
        private TimeSpan mqttConnectionTimeout = TimeSpan.Zero;
        private SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private readonly MqttClientTlsOptions tlsOptions = new MqttClientTlsOptions
        {
            UseTls = true,
            IgnoreCertificateChainErrors = true,
            IgnoreCertificateRevocationErrors = true,
            AllowUntrustedCertificates = true,
            CertificateValidationHandler = (a) => true
        };

        public RabbitService(IOptions<TemperatureAppSettings> temperatureAppSettings, ILogger<RabbitService> logger, IHostApplicationLifetime hostApplicationLifetime, TopicDispatcher topicDispatcher)
        {
            this.temperatureAppSettings = temperatureAppSettings;
            this.logger = logger;
            this.topicDispatcher = topicDispatcher;
            hostApplicationLifetime.ApplicationStopping.Register(Stop);
            Connect(cancellationTokenSource.Token).Wait();
        }

        private void Stop()
        {
            cancellationTokenSource.Cancel();
        }

        private async Task Connect(CancellationToken cancellationToken)
        {
            logger.LogInformation("Connecting to MQTT broker...");
            var mqttFactory = new MqttClientFactory();
            managedMqttClientPublisher = mqttFactory.CreateMqttClient();
            managedMqttClientPublisher.ConnectedAsync += Connected;
            managedMqttClientPublisher.ApplicationMessageReceivedAsync += MessageReceived;
            managedMqttClientPublisher.DisconnectedAsync += Disconnected;
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                if (!managedMqttClientPublisher.IsConnected)
                {
                    await managedMqttClientPublisher.ConnectAsync(BuildMQTTOptions(), cancellationToken);
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
            mqttConnectionTimeout = TimeSpan.Zero;
            await managedMqttClientPublisher.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(MqttTopics.HomeAssistantStatus).Build(), cancellationTokenSource.Token);
            await managedMqttClientPublisher.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(MqttTopics.HeaterOutTemp).Build(), cancellationTokenSource.Token);
        }

        private async Task Disconnected(MqttClientDisconnectedEventArgs e)
        {
            await semaphore.WaitAsync(cancellationTokenSource.Token);
            logger.LogWarning("Disconnected from MQTT broker.");
            if (managedMqttClientPublisher != null)
            {
                while (!managedMqttClientPublisher.IsConnected)
                {
                    if (cancellationTokenSource.IsCancellationRequested)
                    {
                        break;
                    }
                    mqttConnectionTimeout = TimeSpan.FromMilliseconds(Math.Min(mqttConnectionTimeout.TotalMilliseconds * 2 + random.Next(0, 5000), 300000));
                    await Task.Delay((int)mqttConnectionTimeout.TotalMilliseconds, cancellationTokenSource.Token);
                    try
                    {
                        logger.LogInformation("Reconnecting to MQTT broker...");
                        await managedMqttClientPublisher.ConnectAsync(BuildMQTTOptions(), cancellationTokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error reconnecting to MQTT broker.");
                    }
                }
            }
            semaphore.Release();
        }

        private async Task MessageReceived(MqttApplicationMessageReceivedEventArgs e)
        {
            logger.LogInformation("Received MQTT message on topic {topic}", e.ApplicationMessage.Topic);
            await topicDispatcher.Dispatch(e.ApplicationMessage.Topic, e.ApplicationMessage.Payload, cancellationTokenSource.Token);
        }

        public async Task Publish(object data, string topic, CancellationToken cancellationToken)
        {
            if (managedMqttClientPublisher != null && !managedMqttClientPublisher.IsConnected)
            {
                await Connect(cancellationToken);
            }
            try
            {
                if (managedMqttClientPublisher != null)
                {
                    await managedMqttClientPublisher.PublishStringAsync(topic, data.ToString(), cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error publishing to MQTT broker.");
                throw;
            }
        }

        public void Dispose()
        {
            managedMqttClientPublisher?.DisconnectAsync(cancellationToken:cancellationTokenSource.Token).Wait();
            managedMqttClientPublisher?.Dispose();
            managedMqttClientPublisher = null;
            cancellationTokenSource.Dispose();
            semaphore.Dispose();
        }

        private MqttClientOptions BuildMQTTOptions()
        {
            var builder = new MqttClientOptionsBuilder()
                .WithTcpServer(temperatureAppSettings.Value.MqttBroker, temperatureAppSettings.Value.MqttPort)
                .WithProtocolVersion(MqttProtocolVersion.V311)
                .WithTlsOptions(tlsOptions)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
                .WithCleanSession(true)
                .WithCredentials(temperatureAppSettings.Value.MQTTUsername, Encoding.UTF8.GetBytes(temperatureAppSettings.Value.MQTTPassword));
            return builder.Build();
        }
    }
}
