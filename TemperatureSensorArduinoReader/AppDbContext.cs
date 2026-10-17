using Microsoft.EntityFrameworkCore;
using TemperatureSensorArduinoReader.Climate;

namespace TemperatureSensorArduinoReader
{
    public class AppDbContext : DbContext
    {
        public DbSet<Room> Rooms { get; set; }
        public DbSet<SensorState> SensorStates { get; set; }
        public DbSet<SensorReading> SensorReadings { get; set; }
        public DbSet<ClimateDailyMetric> ClimateDailyMetrics { get; set; }
        public DbSet<ClimateDailyReport> ClimateDailyReports { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Room>(entity =>
            {
                entity.HasIndex(e => e.Name).IsUnique();
            });

            modelBuilder.Entity<SensorState>(entity =>
            {
                entity.HasIndex(e => new { e.SensorId, e.Channel }).IsUnique();
            });

            modelBuilder.Entity<SensorReading>(entity =>
            {
                entity.HasIndex(e => new { e.SensorName, e.Timestamp });
            });

            modelBuilder.Entity<ClimateDailyMetric>(entity =>
            {
                entity.HasKey(e => new { e.Day, e.Room, e.Metric });
            });

            modelBuilder.Entity<ClimateDailyReport>(entity =>
            {
                entity.Property(e => e.Facts).HasColumnType("jsonb");
            });
        }
    }
}
