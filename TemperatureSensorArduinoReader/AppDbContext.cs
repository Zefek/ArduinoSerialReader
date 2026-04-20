using Microsoft.EntityFrameworkCore;

namespace TemperatureSensorArduinoReader
{
    public class AppDbContext : DbContext
    {
        public DbSet<Room> Rooms { get; set; }
        public DbSet<SensorState> SensorStates { get; set; }
        public DbSet<SensorReading> SensorReadings { get; set; }

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
        }
    }
}
