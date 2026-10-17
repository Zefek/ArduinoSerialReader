using System.ComponentModel.DataAnnotations;

namespace TemperatureSensorArduinoReader.Climate
{
    public class ClimateDailyMetric
    {
        public DateOnly Day { get; set; }

        [Required]
        [MaxLength(100)]
        public required string Room { get; set; }

        [Required]
        [MaxLength(50)]
        public required string Metric { get; set; }

        public double Value { get; set; }
    }
}
