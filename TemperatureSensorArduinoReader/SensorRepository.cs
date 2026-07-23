using System.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace TemperatureSensorArduinoReader;

internal class SensorRepository
{
    private readonly AppDbContext dbContext;
    private readonly SensorMetrics metrics;

    public SensorRepository(AppDbContext dbContext, SensorMetrics metrics)
    {
        this.dbContext = dbContext;
        this.metrics = metrics;
    }

    public async Task Add(Sensor sensor)
    {
        if (await dbContext.SensorStates.AnyAsync(k => k.SensorId == sensor.Id && k.Channel == sensor.Channel))
            return;

        dbContext.SensorStates.Add(new SensorState
        {
            SensorName = sensor.Name,
            SensorId = sensor.Id,
            Channel = sensor.Channel,
            TemperatureEma = sensor.Temperature,
            AbsoluteHumidityEma = sensor.AbsoluteHumidity,
            LastUpdate = DateTime.UtcNow,
            WindowOpen = sensor.WindowOpen
        });
        await SaveChanges("add");
    }

    public async Task<Sensor?> GetSensor(int id, int channel)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            var sensor = await dbContext.SensorStates
                .Where(k => k.SensorId == id && k.Channel == channel)
                .Select(k => new Sensor(k))
                .FirstOrDefaultAsync();
            metrics.RecordDbOperation("get", Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            return sensor;
        }
        catch
        {
            metrics.RecordDbError("get");
            throw;
        }
    }

    public async Task SaveState(Sensor sensor)
    {
        var state = dbContext.SensorStates.FirstOrDefault(s => s.SensorId == sensor.Id && s.Channel == sensor.Channel);
        if (state != null)
        {
            state.TemperatureEma = sensor.TemperatureEmaValue;
            state.AbsoluteHumidityEma = sensor.AbsoluteHumidityEmaValue;
            state.LastUpdate = sensor.LastUpdateUtc;
            state.WindowOpen = sensor.WindowOpen;
            await SaveChanges("save_state");
        }
    }

    public async Task SaveReading(Sensor sensor)
    {
        dbContext.SensorReadings.Add(new SensorReading
        {
            SensorName = sensor.Name,
            Timestamp = DateTime.UtcNow,
            Temperature = sensor.Temperature,
            Humidity = sensor.Humidity,
            BatteryLow = sensor.BatteryLow,
            DewPoint = sensor.DewPoint,
            AbsoluteHumidity = sensor.AbsoluteHumidity,
            TemperatureTrend = sensor.TemperatureTrend,
            HumidityTrend = sensor.HumidityTrend,
            WindowOpen = sensor.WindowOpen
        });
        await SaveChanges("save_reading");
    }

    private async Task SaveChanges(string operation)
    {
        var start = Stopwatch.GetTimestamp();
        try
        {
            await dbContext.SaveChangesAsync();
            metrics.RecordDbOperation(operation, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
        catch
        {
            metrics.RecordDbError(operation);
            throw;
        }
    }
}
