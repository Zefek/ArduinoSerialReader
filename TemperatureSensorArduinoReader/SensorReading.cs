using System.ComponentModel.DataAnnotations;

namespace TemperatureSensorArduinoReader
{
    public class SensorReading
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string SensorName { get; set; }

        public DateTime Timestamp { get; set; }

        public double Temperature { get; set; }

        public int Humidity { get; set; }

        public bool BatteryLow { get; set; }

        public double DewPoint { get; set; }

        public double AbsoluteHumidity { get; set; }

        public double TemperatureTrend { get; set; }

        public double HumidityTrend { get; set; }

        public bool WindowOpen { get; set; }
    }
}
