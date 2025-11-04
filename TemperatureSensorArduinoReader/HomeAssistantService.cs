using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using System.Net.WebSockets;
using System.Text;

namespace TemperatureSensorArduinoReader;
public class HomeAssistantService : BackgroundService
{
    private ClientWebSocket clientWebSocket = null;
    private readonly RoomRepository roomRepository;
    private readonly IOptions<TemperatureAppSettings> options;
    private readonly RabbitService rabbitService;
    private readonly ILogger<HomeAssistantService> logger;
    private static readonly Random random = new();
    private int messageId = 2;
    private bool connected = false;
    private DateTime? lastConnectionTry = null;
    private TimeSpan connectionTimeout = TimeSpan.Zero;

    public HomeAssistantService(RoomRepository roomRepository, IOptions<TemperatureAppSettings> options, RabbitService rabbitService, ILogger<HomeAssistantService> logger)
    {
        this.roomRepository = roomRepository;
        this.options = options;
        this.rabbitService = rabbitService;
        this.logger = logger;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!connected)
            {
                await Connect(stoppingToken);
            }
            if (!connected) 
            {
                continue;
            }
            var message = await ReceiveMessage(stoppingToken);
            if (message != null)
            {
                if (message.type == "event" && message.@event.event_type == "device_registry_updated" && message.@event.data.action == "update")
                {
                    var deviceId = message.@event.data.device_id.ToString();
                    await SendMessage(new
                    {
                        id = messageId++,
                        type = "config/device_registry/list"
                    }, stoppingToken);
                    var devices = await ReceiveMessage(stoppingToken);
                    foreach (var device in devices.result)
                    {
                        if (device.id == deviceId && device.name.ToString().StartsWith("TX07K-TXC/") && !string.IsNullOrEmpty(device.area_id.ToString()))
                        {
                            var sensorName = device.name.ToString().Substring("TX07K-TXC/".Length, device.name.ToString().Length - "TX07K-TXC/".Length);
                            var room = roomRepository.GetRooms().FirstOrDefault(k => k.Name == device.area_id.ToString());
                            if (room == null)
                            {
                                roomRepository.AddRoom(device.area_id.ToString(), sensorName);
                            }
                            else
                            {
                                roomRepository.UpdateRoomSensor(room.Name, sensorName);
                                await rabbitService.Publish("", "homeassistant/sensor/" + sensorName + "_temperature/config", stoppingToken);
                            }
                        }
                    }
                }
            }
            // Keep the service running
            await Task.Delay(10, stoppingToken);
        }
    }

    private async Task Connect(CancellationToken stoppingToken)
    {
        if(lastConnectionTry != null && DateTime.Now - lastConnectionTry < connectionTimeout)
        {
            return;
        }
        try
        {
            logger.LogInformation("Connecting to Home Assistant WebSocket...");
            clientWebSocket = new ClientWebSocket();
            clientWebSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
            await clientWebSocket.ConnectAsync(new Uri(options.Value.HomeAssistantWebSocket), stoppingToken);
            var resultAuthRequired = await ReceiveMessage(stoppingToken, force: true);
            await SendMessage(new { type = "auth", access_token = options.Value.HomeAssistantToken }, stoppingToken, force: true);
            var resultOk = await ReceiveMessage(stoppingToken, force: true);
            if (resultOk.type != "auth_ok")
            {
                string message = JsonConvert.SerializeObject(resultOk);
                logger.LogError("Error authenticating to Home Assistant WebSocket {message}", message);
                lastConnectionTry = DateTime.Now;
                connectionTimeout = TimeSpan.FromMilliseconds(Math.Min((connectionTimeout * 2 + TimeSpan.FromSeconds(random.Next(5))).TotalMilliseconds, TimeSpan.FromMinutes(5).TotalMilliseconds));
                clientWebSocket.Dispose();
                clientWebSocket = null;
                return;
            }
            logger.LogInformation("Authenticated to Home Assistant WebSocket");
            await SendMessage(new
            {
                id = 1,
                type = "subscribe_events",
                event_type = "device_registry_updated"
            }, stoppingToken, force: true);
            logger.LogInformation("Subscribed to device_registry_updated events");
            lastConnectionTry = null;
            connectionTimeout = TimeSpan.Zero;
            connected = true;
            logger.LogInformation("Connected to Home Assistant WebSocket");
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Error connecting to Home Assistant WebSocket");
            lastConnectionTry = DateTime.Now;
            connectionTimeout = TimeSpan.FromMilliseconds(Math.Min((connectionTimeout * 2 + TimeSpan.FromSeconds(random.Next(5))).TotalMilliseconds, TimeSpan.FromMinutes(5).TotalMilliseconds));
            clientWebSocket?.Dispose();
            clientWebSocket = null;
        }
    }

    private async Task SendMessage(object value, CancellationToken cancellationToken, bool force = false)
    {
        logger.LogDebug("Sending message to Home Assistant: {message}", System.Text.Json.JsonSerializer.Serialize(value));
        if (connected || force)
        {
            try
            {
                await clientWebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(value))), WebSocketMessageType.Text, true, cancellationToken);
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "Error sending message to Home Assistant");
                clientWebSocket.Dispose();
                clientWebSocket = null;
                connected = false;
            }
        }
    }

    private async Task<dynamic> ReceiveMessage(CancellationToken cancellationToken, bool force = false)
    {
        if (connected || force)
        {
            try
            {
                var endOfMessage = false;
                var sb = new StringBuilder();
                do
                {
                    var buffer = new byte[4096];
                    var result = await clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    endOfMessage = result.EndOfMessage;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!endOfMessage);
                logger.LogDebug("Message received from Home Assistant: {message}", sb.ToString());
                return JsonConvert.DeserializeObject(sb.ToString());
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "Error receiving message from Home Assistant");
                clientWebSocket.Dispose();
                clientWebSocket = null;
                connected = false;
                return null;
            }
        }
        return null;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        clientWebSocket.Abort();
        clientWebSocket.Dispose();
        return base.StopAsync(cancellationToken);
    }
}
