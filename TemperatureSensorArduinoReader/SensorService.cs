using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace TemperatureSensorArduinoReader
{
    internal class SensorService
    {
        private const string SensorConfigPrefix = "homeassistant/sensor/";
        private const string BinarySensorConfigPrefix = "homeassistant/binary_sensor/";

        private readonly RoomRepository roomRepository;
        private readonly RabbitService rabbitService;
        private readonly ILogger<SensorService> logger;

        public SensorService(RoomRepository roomRepository, RabbitService rabbitService, ILogger<SensorService> logger)
        {
            this.roomRepository = roomRepository;
            this.rabbitService = rabbitService;
            this.logger = logger;
        }

        private async Task SendSensorDiscovery(string sensorName, CancellationToken cancellationToken)
        {
            logger.LogInformation("Sensor {Sensor} not assigned to any room, but ForcedTransmition is set, publishing anyway.", sensorName);
            await rabbitService.Publish(JsonConvert.SerializeObject(HomeAssistantSensor.CreateTemperature(sensorName)), SensorConfigPrefix + sensorName + "_temperature/config", cancellationToken);
            await rabbitService.Publish(JsonConvert.SerializeObject(HomeAssistantSensor.CreateHumidity(sensorName)), SensorConfigPrefix + sensorName + "_humidity/config", cancellationToken);
            await rabbitService.Publish(JsonConvert.SerializeObject(HomeAssistantSensor.CreateBattery(sensorName)), BinarySensorConfigPrefix + sensorName + "_battery/config", cancellationToken);
            await rabbitService.Publish(JsonConvert.SerializeObject(HomeAssistantSensor.CreateTrend(sensorName)), SensorConfigPrefix + sensorName + "_trend/config", cancellationToken);
            await rabbitService.Publish(JsonConvert.SerializeObject(HomeAssistantSensor.CreateDewPoint(sensorName)), SensorConfigPrefix + sensorName + "_dew_point/config", cancellationToken);
            await rabbitService.Publish(JsonConvert.SerializeObject(HomeAssistantSensor.CreateAbsoluteHumidity(sensorName)), SensorConfigPrefix + sensorName + "_absolute_humidity/config", cancellationToken);
            await rabbitService.Publish(JsonConvert.SerializeObject(HomeAssistantSensor.CreateTemperatureTrend(sensorName)), SensorConfigPrefix + sensorName + "_temperature_trend/config", cancellationToken);
            await rabbitService.Publish(JsonConvert.SerializeObject(HomeAssistantSensor.CreateHumidityTrend(sensorName)), SensorConfigPrefix + sensorName + "_humidity_trend/config", cancellationToken);
            await rabbitService.Publish(JsonConvert.SerializeObject(HomeAssistantSensor.CreateWindowOpen(sensorName)), BinarySensorConfigPrefix + sensorName + "_window_open/config", cancellationToken);
        }

        public async Task SendAllSensorsDiscovery(CancellationToken cancellationToken)
        {
            foreach (var room in roomRepository.GetRooms())
            {
                await SendSensorDiscovery(room.SensorName, cancellationToken);
            }
        }

        public async Task PublishSensorData(Sensor sensor, CancellationToken cancellationToken)
        {
            var rooms = roomRepository.GetRooms();
            var topic = sensor.Name;
            var room = rooms.FirstOrDefault(k => k.SensorName == sensor.Name) ?? rooms.FirstOrDefault(k => k.SensorNewName == sensor.Name);
            if (room != null)
            {
                logger.LogInformation("{Topic} assigned to room {Room}, publishing to topic {SensorName}", topic, room.Name, room.SensorName);
                topic = room.SensorName;
            }
            if (room == null && sensor.ForcedTransmition)
            {
                await SendSensorDiscovery(topic, cancellationToken);
            }
            string trend;
            if (sensor.TemperatureUp)
            {
                trend = "↗";
            }
            else if (sensor.TemperatureDown)
            {
                trend = "↘";
            }
            else
            {
                trend = "→";
            }
            var body = JsonConvert.SerializeObject(new
            {
                temperature = Math.Round(sensor.Temperature, 1),
                humidity = sensor.Humidity,
                battery = sensor.BatteryLow ? "ON" : "OFF",
                trend,
                dewPoint = Math.Round(sensor.DewPoint, 1),
                absoluteHumidity = Math.Round(sensor.AbsoluteHumidity, 1),
                temperatureTrend = Math.Round(sensor.TemperatureTrend, 1),
                humidityTrend = Math.Round(sensor.HumidityTrend, 1),
                windowOpen = sensor.WindowOpen && (room?.HasWindow ?? false) ? "ON" : "OFF"
            });

            logger.LogInformation("Publishing data for sensor {Sensor} to topic TX07KTXC/{Topic}/state: {Data}", sensor.Name, topic, body);
            await rabbitService.Publish(body, "TX07KTXC/" + topic + "/state", cancellationToken);
        }
    }
}
