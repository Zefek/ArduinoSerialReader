namespace TemperatureSensorArduinoReader
{
    public class RoomRepository
    {
        private readonly AppDbContext dbContext;

        public RoomRepository(AppDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        public List<Room> GetRooms()
        {
            return dbContext.Rooms.ToList();
        }

        public void AddRoom(string roomName, string sensorName)
        {
            dbContext.Rooms.Add(new Room { Name = roomName, SensorName = sensorName });
            dbContext.SaveChanges();
        }

        public void UpdateRoomSensor(string roomName, string sensorName)
        {
            var room = dbContext.Rooms.FirstOrDefault(k => k.Name == roomName);
            if (room != null)
            {
                room.SensorNewName = sensorName;
                dbContext.SaveChanges();
            }
        }
    }
}
