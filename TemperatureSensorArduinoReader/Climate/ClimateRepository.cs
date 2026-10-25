using Microsoft.EntityFrameworkCore;

namespace TemperatureSensorArduinoReader.Climate;

class ClimateRepository
{
    readonly AppDbContext db;

    public ClimateRepository(AppDbContext db)
    {
        this.db = db;
    }

    public bool UseHistory { get; set; } = true;

    public async Task<List<(string Name, bool HasWindow)>> Rooms()
    {
        var rooms = await db.Rooms.AsNoTracking().OrderBy(r => r.Name).Select(r => new { r.Name, r.HasWindow }).ToListAsync();
        return rooms.Select(r => (r.Name, r.HasWindow)).ToList();
    }

    public async Task<List<ClimateReading>> Readings(DateTimeOffset from, DateTimeOffset to)
    {
        var sensors = new Dictionary<string, string>();
        foreach (var room in await db.Rooms.AsNoTracking().ToListAsync())
        {
            sensors.TryAdd(room.SensorName, room.Name);
            if (room.SensorNewName is { Length: > 0 } newName)
            {
                sensors.TryAdd(newName, room.Name);
            }
        }
        var names = sensors.Keys.ToList();
        var fromUtc = from.UtcDateTime;
        var toUtc = to.UtcDateTime;
        var readings = await db.SensorReadings.AsNoTracking()
            .Where(s => names.Contains(s.SensorName) && s.Timestamp >= fromUtc && s.Timestamp < toUtc)
            .OrderBy(s => s.Timestamp)
            .Select(s => new { s.SensorName, s.Timestamp, s.Temperature, s.Humidity, s.AbsoluteHumidity, s.WindowOpen, s.BatteryLow })
            .ToListAsync();
        return readings
            .Select(s => new ClimateReading(
                sensors[s.SensorName],
                new DateTimeOffset(DateTime.SpecifyKind(s.Timestamp, DateTimeKind.Utc)),
                s.Temperature,
                s.Humidity,
                s.AbsoluteHumidity,
                s.WindowOpen,
                s.BatteryLow))
            .OrderBy(r => r.Room, StringComparer.Ordinal)
            .ThenBy(r => r.Ts)
            .ToList();
    }

    public async Task<Dictionary<(string, string), double>> MetricValues(DateOnly day) =>
        !UseHistory
            ? new()
            : await db.ClimateDailyMetrics.AsNoTracking()
                .Where(m => m.Day == day)
                .ToDictionaryAsync(m => (m.Room, m.Metric), m => m.Value);

    public async Task<Dictionary<(string, string), double>> MetricAverages(DateOnly day)
    {
        if (!UseHistory)
        {
            return new();
        }
        var from = day.AddDays(-7);
        var averages = await db.ClimateDailyMetrics.AsNoTracking()
            .Where(m => m.Day >= from && m.Day < day)
            .GroupBy(m => new { m.Room, m.Metric })
            .Select(g => new { g.Key.Room, g.Key.Metric, Value = g.Average(m => m.Value) })
            .ToListAsync();
        return averages.ToDictionary(a => (a.Room, a.Metric), a => a.Value);
    }

    public async Task Save(DateOnly day, ClimateReportBuilder builder)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.ClimateDailyMetrics.Where(m => m.Day == day).ExecuteDeleteAsync();
        db.ClimateDailyMetrics.AddRange(builder.Metrics
            .GroupBy(m => (m.Room, m.Name))
            .Select(g => new ClimateDailyMetric { Day = day, Room = g.Key.Room, Metric = g.Key.Name, Value = g.Last().Value }));

        var report = await db.ClimateDailyReports.FindAsync(day);
        if (report == null)
        {
            report = new ClimateDailyReport { Day = day, Status = builder.Status, Facts = builder.Facts!.ToJsonString() };
            db.ClimateDailyReports.Add(report);
        }
        report.Status = builder.Status;
        report.Facts = builder.Facts!.ToJsonString();
        report.CreatedAt = DateTime.UtcNow;
        if (builder.Comment != null)
        {
            report.Comment = builder.Comment.Text;
            report.CommentModel = builder.Comment.Model;
            report.CommentTokensIn = builder.Comment.TokensIn;
            report.CommentTokensOut = builder.Comment.TokensOut;
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
