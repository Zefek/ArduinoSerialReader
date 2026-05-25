namespace TemperatureSensorArduinoReader
{
    internal class RoomService
    {
        private readonly RoomRepository roomRepository;
        private readonly RabbitService rabbitService;

        public RoomService(RoomRepository roomRepository, RabbitService rabbitService)
        {
            this.roomRepository=roomRepository;
            this.rabbitService = rabbitService;
        }

        public async Task AddOrUpdateRoom(string areaId, string sensorName, CancellationToken cancellationToken)
        {
            var room = roomRepository.GetRooms().FirstOrDefault(k => k.Name == areaId);
            if (room == null)
            {
                roomRepository.AddRoom(areaId, sensorName);
            }
            else
            {
                roomRepository.UpdateRoomSensor(room.Name, sensorName);
                await rabbitService.Publish("", "homeassistant/sensor/" + sensorName + "_temperature/config", cancellationToken);
            }
        }
    }
}
