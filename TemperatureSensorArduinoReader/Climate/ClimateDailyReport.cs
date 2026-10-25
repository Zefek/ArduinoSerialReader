using System.ComponentModel.DataAnnotations;

namespace TemperatureSensorArduinoReader.Climate
{
    public class ClimateDailyReport
    {
        [Key]
        public DateOnly Day { get; set; }

        [Required]
        [MaxLength(10)]
        public required string Status { get; set; }

        [Required]
        public required string Facts { get; set; }

        public string? Comment { get; set; }

        [MaxLength(100)]
        public string? CommentModel { get; set; }

        public int? CommentTokensIn { get; set; }

        public int? CommentTokensOut { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
