using Microsoft.EntityFrameworkCore;

namespace TemperatureSensorArduinoReader;

internal class SensorRepository
{
    private readonly AppDbContext dbContext;

    public SensorRepository(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
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
            HumidityEma = sensor.Humidity,
            LastUpdate = DateTime.UtcNow,
            WindowOpen = sensor.WindowOpen
        });
        await dbContext.SaveChangesAsync();
    }

    public async Task<Sensor?> GetSensor(int id, int channel)
    {
        return await dbContext.SensorStates
            .Where(k => k.SensorId == id && k.Channel == channel)
            .Select(k => new Sensor(k))
            .FirstOrDefaultAsync();
    }

    public async Task SaveState(Sensor sensor)
    {
        var state = dbContext.SensorStates.FirstOrDefault(s => s.SensorId == sensor.Id && s.Channel == sensor.Channel);
        if (state != null)
        {
            state.TemperatureEma = sensor.TemperatureEmaValue;
            state.HumidityEma = sensor.HumidityEmaValue;
            state.LastUpdate = sensor.LastUpdateUtc;
            state.WindowOpen = sensor.WindowOpen;
            await dbContext.SaveChangesAsync();
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
        await dbContext.SaveChangesAsync();
    }
}
