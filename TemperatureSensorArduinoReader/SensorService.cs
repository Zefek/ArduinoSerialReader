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

        public SensorService(RoomRepository roomRepository, RabbitService rabbitService)
        {
            this.roomRepository=roomRepository;
            this.rabbitService=rabbitService;
        }
        public async Task PublishSensorData(Sensor sensor)
        {
            var rooms = roomRepository.GetRooms();
            var room = rooms.FirstOrDefault(k => k.SensorChannel == sensor.Channel && k.SensorId == sensor.Id);
            if(room != null)
            {
                var s = "";
                foreach(var d in sensor.Data)
                {
                    s+=d.ToString("X2");
                }
                var data = Encoding.UTF8.GetString(sensor.Data);
                var b = Encoding.ASCII.GetBytes(data);
                await rabbitService.Publish(s, room.TopicName);
            }
        }
    }
}
