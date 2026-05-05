namespace TemperatureSensorArduinoReader;

internal class SensorRepository
{
    private readonly List<Sensor> sensors = new();
    private readonly AppDbContext dbContext;

    public SensorRepository(AppDbContext dbContext)
    {
        this.dbContext = dbContext;
        LoadStatesFromDb();
    }

    private void LoadStatesFromDb()
    {
        var states = dbContext.SensorStates.ToList();
        foreach (var state in states)
        {
            var sensor = new Sensor(state);
            sensors.Add(sensor);
        }
    }

    public void Add(Sensor sensor)
    {
        if (sensors.Any(k => k.Id == sensor.Id && k.Channel == sensor.Channel))
            return;

        sensors.Add(sensor);

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
        dbContext.SaveChanges();
    }

    public Sensor GetSensor(int id, int channel)
    {
        return sensors.FirstOrDefault(k => k.Id == id && k.Channel == channel);
    }

    public void SaveState(Sensor sensor)
    {
        var state = dbContext.SensorStates.FirstOrDefault(s => s.SensorId == sensor.Id && s.Channel == sensor.Channel);
        if (state != null)
        {
            state.TemperatureEma = sensor.TemperatureEmaValue;
            state.HumidityEma = sensor.HumidityEmaValue;
            state.LastUpdate = sensor.LastUpdateUtc;
            state.WindowOpen = sensor.WindowOpen;
            dbContext.SaveChanges();
        }
    }

    public void SaveReading(Sensor sensor)
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
        dbContext.SaveChanges();
    }
}
