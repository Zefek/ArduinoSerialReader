using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Formatter;
using System.Linq;
using System.Net.Security;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using TemperatureSensorArduinoReader.TopicStrategies;

namespace TemperatureSensorArduinoReader
{
    public sealed class RabbitService : IDisposable
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private IMqttClient? managedMqttClientPublisher;
        private static readonly Random random = new();
        private readonly IOptions<TemperatureAppSettings> temperatureAppSettings;
        private readonly ILogger<RabbitService> logger;
        private readonly TopicDispatcher topicDispatcher;
        private readonly SensorMetrics metrics;
        private TimeSpan mqttConnectionTimeout = TimeSpan.Zero;
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private readonly MqttClientTlsOptions tlsOptions;

        public RabbitService(IOptions<TemperatureAppSettings> temperatureAppSettings, ILogger<RabbitService> logger, IHostApplicationLifetime hostApplicationLifetime, TopicDispatcher topicDispatcher, SensorMetrics metrics)
        {
            this.temperatureAppSettings = temperatureAppSettings;
            this.logger = logger;
            this.topicDispatcher = topicDispatcher;
            this.metrics = metrics;
            tlsOptions = new MqttClientTlsOptions
            {
                UseTls = true,
                CertificateValidationHandler = ValidateCertificate
            };
            hostApplicationLifetime.ApplicationStopping.Register(Stop);
            Connect(cancellationTokenSource.Token).Wait(hostApplicationLifetime.ApplicationStopping);
        }

        private bool ValidateCertificate(MqttClientCertificateValidationEventArgs context)
        {
            if (context.SslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            if (context.SslPolicyErrors == SslPolicyErrors.RemoteCertificateChainErrors
                && context.Chain != null
                && context.Chain.ChainStatus.All(s => s.Status is X509ChainStatusFlags.NoError or X509ChainStatusFlags.RevocationStatusUnknown or X509ChainStatusFlags.OfflineRevocation))
            {
                logger.LogInformation("Accepting MQTT TLS certificate {Subject}; revocation status could not be checked.", context.Certificate?.Subject);
                return true;
            }

            logger.LogWarning("MQTT TLS certificate validation errors: {Errors} for {Subject}", context.SslPolicyErrors, context.Certificate?.Subject);
            if (context.Chain != null)
            {
                var chainStatus = string.Join("; ", context.Chain.ChainStatus.Select(s => $"{s.Status}: {s.StatusInformation?.Trim()}"));
                logger.LogWarning("MQTT TLS chain status: {ChainStatus}", chainStatus);
                foreach (var element in context.Chain.ChainElements)
                {
                    var elementStatus = string.Join(", ", element.ChainElementStatus.Select(s => s.Status.ToString()));
                    logger.LogWarning("MQTT TLS chain element {Subject}: {Status}", element.Certificate.Subject, string.IsNullOrEmpty(elementStatus) ? "OK" : elementStatus);
                }
            }
            return false;
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
            metrics.SetMqttConnected(true);
            mqttConnectionTimeout = TimeSpan.Zero;
            await managedMqttClientPublisher.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(MqttTopics.HomeAssistantStatus).Build(), cancellationTokenSource.Token);
            await managedMqttClientPublisher.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(MqttTopics.HeaterOutTemp).Build(), cancellationTokenSource.Token);
            await managedMqttClientPublisher.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(MqttTopics.GarageTemperature).Build(), cancellationTokenSource.Token);
        }

        private async Task Disconnected(MqttClientDisconnectedEventArgs e)
        {
            await semaphore.WaitAsync(cancellationTokenSource.Token);
            logger.LogWarning("Disconnected from MQTT broker.");
            metrics.SetMqttConnected(false);
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
                        metrics.RecordMqttReconnect();
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
            logger.LogInformation("Received MQTT message on topic {Topic}", e.ApplicationMessage.Topic);
            await topicDispatcher.Dispatch(e.ApplicationMessage.Topic, e.ApplicationMessage.Payload, cancellationTokenSource.Token);
        }

        public async Task Publish(object data, string topic, CancellationToken cancellationToken)
        {
            if (managedMqttClientPublisher != null && !managedMqttClientPublisher.IsConnected)
            {
                await Connect(cancellationToken);
            }
            var start = Stopwatch.GetTimestamp();
            try
            {
                if (managedMqttClientPublisher != null)
                {
                    await managedMqttClientPublisher.PublishStringAsync(topic, data.ToString(), cancellationToken: cancellationToken);
                    metrics.RecordMqttPublish(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
                }
            }
            catch (Exception)
            {
                metrics.RecordMqttPublishError();
                throw;
            }
        }

        public void Dispose()
        {
            managedMqttClientPublisher?.DisconnectAsync(cancellationToken: cancellationTokenSource.Token).Wait(CancellationToken.None);
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
