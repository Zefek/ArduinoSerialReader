namespace TemperatureSensorArduinoReader
{
    internal class SensorData
    {
        public required int Id { get; set; }
        public required int Humidity { get; set; }
        public required int Channel { get; set; }
        public bool BatteryLow { get; set; } = false;
        public bool TemperatureDown { get; set; } = false;
        public bool TemperatureUp { get; set; } = false;
        public bool ForcedTransmition { get; set; } = false;
        public required double Temperature { get; set; }
    }
}
