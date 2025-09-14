using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TemperatureSensorArduinoReader
{
    public class RoomRepository
    {
        public List<Room>GetRooms() 
        {
            return JsonConvert.DeserializeObject<List<Room>>(File.ReadAllText("rooms.json"));
        }

        public void AddRoom(string roomName, string sensorName)
        {
            var rooms = JsonConvert.DeserializeObject<List<Room>>(File.ReadAllText("rooms.json"));
            rooms.Add(new Room { Name = roomName, SensorName = sensorName });
            File.WriteAllText("rooms.json", JsonConvert.SerializeObject(rooms));
        }

        public void UpdateRoomSensor(string roomName, string sensorName)
        {
            var rooms = JsonConvert.DeserializeObject<List<Room>>(File.ReadAllText("rooms.json"));
            var room = rooms.FirstOrDefault(k => k.Name == roomName);
            rooms.Remove(room);
            room.SensorNewName = sensorName;
            rooms.Add(room);
            File.WriteAllText("rooms.json", JsonConvert.SerializeObject(rooms));
        }
    }
}
