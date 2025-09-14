using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net.WebSockets;
using System.Text;

namespace TemperatureSensorArduinoReader;
public class HomeAssistantService : BackgroundService
{
    private readonly ClientWebSocket clientWebSocket = new ClientWebSocket();
    private readonly RoomRepository roomRepository;
    private readonly IOptions<TemperatureAppSettings> options;
    private int messageId = 2;

    public HomeAssistantService(RoomRepository roomRepository, IOptions<TemperatureAppSettings> options)
    {
        this.roomRepository = roomRepository;
        this.options = options;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        clientWebSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
        await clientWebSocket.ConnectAsync(new Uri(options.Value.HomeAssistantWebSocket), stoppingToken);
        var resultAuthRequired = await ReceiveMessage(stoppingToken);
        await SendMessage(new { type = "auth", access_token = options.Value.HomeAssistantToken }, stoppingToken);
        var resultOk = await ReceiveMessage(stoppingToken);
        if (resultOk.type != "auth_ok")
        {
            return;
        }
        await SendMessage(new
        {
            id = 1,
            type = "subscribe_events",
            event_type = "device_registry_updated"
        }, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
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
                            if(room == null)
                            {
                                roomRepository.AddRoom(device.area_id.ToString(), sensorName);
                            }
                            else
                            {
                                roomRepository.UpdateRoomSensor(room.Name, sensorName);
                            }
                        }
                    }
                }
                //Console.WriteLine($"Message received: {message}");
            }
            // Keep the service running
            await Task.Delay(10, stoppingToken);
        }
    }

    private async Task SendMessage(object value, CancellationToken cancellationToken)
    {
        await clientWebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(value))), WebSocketMessageType.Text, true, cancellationToken);
    }

    private async Task<dynamic> ReceiveMessage(CancellationToken cancellationToken)
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
        return JsonConvert.DeserializeObject(sb.ToString());
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        clientWebSocket.Abort();
        clientWebSocket.Dispose();
        return base.StopAsync(cancellationToken);
    }
}
