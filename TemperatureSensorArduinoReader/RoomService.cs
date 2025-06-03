using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TemperatureSensorArduinoReader
{
    internal class RoomService
    {
        private readonly RoomRepository roomRepository;

        public RoomService(RoomRepository roomRepository)
        {
            this.roomRepository=roomRepository;
        }
        public void Register(SensorData sensorData, int roomId)
        {
            /*
            var rooms = roomRepository.GetRooms();
            var room = rooms.First(k=>k.Id == roomId);
            room.SensorChannel = sensorData.C;
            room.SensorId = sensorData.S;*/
        }
    }
}
