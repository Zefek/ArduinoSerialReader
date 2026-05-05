using System.ComponentModel.DataAnnotations;

namespace TemperatureSensorArduinoReader
{
    public class Room
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required]
        [MaxLength(50)]
        public string SensorName { get; set; }

        [MaxLength(50)]
        public string? SensorNewName { get; set; }
    }
}
