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
        }
        public async Task PublishSensorData(Sensor sensor)
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
                logger.LogInformation("Sensor {sensor} not assigned to any room, but ForcedTransmition is set, publishing anyway.", sensor.Name);
                await rabbitService.Publish(JsonConvert.SerializeObject(HomeAssistantSensor.CreateTemperature(sensor.Name)), "homeassistant/sensor/" + sensor.Name + "_temperature/config");
                await rabbitService.Publish(JsonConvert.SerializeObject(HomeAssistantSensor.CreateHumidity(sensor.Name)), "homeassistant/sensor/" + sensor.Name + "_humidity/config");
                await rabbitService.Publish(JsonConvert.SerializeObject(HomeAssistantSensor.CreateBattery(sensor.Name)), "homeassistant/sensor/" + sensor.Name + "_battery/config");
                await rabbitService.Publish(JsonConvert.SerializeObject(HomeAssistantSensor.CreateTrend(sensor.Name)), "homeassistant/sensor/" + sensor.Name + "_trend/config");
            }
            var s = "";
            logger.LogInformation("Publishing data for sensor {sensor} to topic TX07KTXC/{topic}/state: {data}", sensor.Name, topic, BitConverter.ToString(sensor.Data).Replace("-", ""));
            foreach (var d in sensor.Data)
            {
                s += d.ToString("X2");
            }
            await rabbitService.Publish(s, "TX07KTXC/" + topic+"/state");
        }
    }
}
