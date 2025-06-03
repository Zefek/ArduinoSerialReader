using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TemperatureSensorArduinoReader
{
    internal class RoomRepository
    {
        public List<Room>GetRooms() 
        {
            return JsonConvert.DeserializeObject<List<Room>>(File.ReadAllText("rooms.json"));
        }
    }
}
