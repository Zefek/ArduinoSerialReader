using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TemperatureSensorArduinoReader
{
    internal class SensorService
    {
        private readonly RoomRepository roomRepository;
        private readonly RabbitService rabbitService;
        private readonly ILogger<SensorService> logger;

        public SensorService(RoomRepository roomRepository, RabbitService rabbitService, ILogger<SensorService> logger)
        {
            this.roomRepository = roomRepository;
            this.rabbitService = rabbitService;
            this.logger = logger;
            this.rabbitService.HomeAssistantOnline += RabbitService_HomeAssistantOnline;
        }

        private async Task SendSensorDiscovery(string sensorName, CancellationToken cancellationToken)
        {
            logger.LogInformation("Sensor {sensor} not assigned to any room, but ForcedTransmition is set, publishing anyway.", sensorName);
            await rabbitService.Publish(JsonConvert.SerializeObject(HomeAssistantSensor.CreateTemperature(sensorName)), "homeassistant/sensor/" + sensorName + "_temperature/config", cancellationToken);
            await rabbitService.Publish(JsonConvert.SerializeObject(HomeAssistantSensor.CreateHumidity(sensorName)), "homeassistant/sensor/" + sensorName + "_humidity/config", cancellationToken);
            await rabbitService.Publish(JsonConvert.SerializeObject(HomeAssistantSensor.CreateBattery(sensorName)), "homeassistant/sensor/" + sensorName + "_battery/config", cancellationToken);
            await rabbitService.Publish(JsonConvert.SerializeObject(HomeAssistantSensor.CreateTrend(sensorName)), "homeassistant/sensor/" + sensorName + "_trend/config", cancellationToken);
        }

        private async void RabbitService_HomeAssistantOnline(object? sender, EventArgs e)
        {
            await SendAllSensorsDiscovery(CancellationToken.None);
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
            if(room != null)
            {
                logger.LogInformation(topic + " assigned to room " + room.Name + ", publishing to topic " + room.SensorName);
                topic = room.SensorName;
            }
            if (room == null && sensor.ForcedTransmition)
            {
                await SendSensorDiscovery(topic, cancellationToken);
            }
            var s = "";
            logger.LogInformation("Publishing data for sensor {sensor} to topic TX07KTXC/{topic}/state: {data}", sensor.Name, topic, BitConverter.ToString(sensor.Data).Replace("-", ""));
            foreach (var d in sensor.Data)
            {
                s += d.ToString("X2");
            }
            await rabbitService.Publish(s, "TX07KTXC/" + topic+"/state", cancellationToken);
        }
    }
}
