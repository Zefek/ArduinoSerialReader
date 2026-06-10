using System.ComponentModel.DataAnnotations;

namespace TemperatureSensorArduinoReader
{
    public class SensorState
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public required string SensorName { get; set; }

        public int SensorId { get; set; }

        public int Channel { get; set; }

        public double TemperatureEma { get; set; }

        public double AbsoluteHumidityEma { get; set; }

        public DateTime LastUpdate { get; set; }

        public bool WindowOpen { get; set; }
    }
}
