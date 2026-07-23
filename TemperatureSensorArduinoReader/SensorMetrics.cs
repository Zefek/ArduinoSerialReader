using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace TemperatureSensorArduinoReader;

public class SensorMetrics
{
    public const string MeterName = "TemperatureSensorArduinoReader";

    private readonly record struct Snapshot(
        double Temperature,
        double Humidity,
        double AbsoluteHumidity,
        double DewPoint,
        double TemperatureTrend,
        double HumidityTrend,
        double TemperatureEma,
        double AbsoluteHumidityEma,
        bool WindowOpen,
        bool BatteryLow);

    private readonly ConcurrentDictionary<string, Snapshot> sensors = new();

    private readonly Counter<long> readingsProcessed;
    private readonly Counter<long> readingErrors;

    private readonly Counter<long> mqttPublished;
    private readonly Counter<long> mqttPublishErrors;
    private readonly Counter<long> mqttReconnects;
    private readonly Histogram<double> mqttPublishDuration;
    private int mqttConnected;

    private readonly Counter<long> dbOperations;
    private readonly Counter<long> dbErrors;
    private readonly Histogram<double> dbOperationDuration;

    public SensorMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        readingsProcessed = meter.CreateCounter<long>("asr.readings.processed", unit: "{reading}");
        readingErrors = meter.CreateCounter<long>("asr.readings.errors", unit: "{error}");

        mqttPublished = meter.CreateCounter<long>("asr.mqtt.published", unit: "{message}");
        mqttPublishErrors = meter.CreateCounter<long>("asr.mqtt.publish.errors", unit: "{error}");
        mqttReconnects = meter.CreateCounter<long>("asr.mqtt.reconnects", unit: "{reconnect}");
        mqttPublishDuration = meter.CreateHistogram<double>("asr.mqtt.publish.duration", unit: "ms");
        meter.CreateObservableGauge("asr.mqtt.connected", () => mqttConnected);

        dbOperations = meter.CreateCounter<long>("asr.db.operations", unit: "{operation}");
        dbErrors = meter.CreateCounter<long>("asr.db.errors", unit: "{error}");
        dbOperationDuration = meter.CreateHistogram<double>("asr.db.operation.duration", unit: "ms");

        meter.CreateObservableGauge("asr.sensor.temperature", () => Observe(s => s.Temperature));
        meter.CreateObservableGauge("asr.sensor.humidity", () => Observe(s => s.Humidity));
        meter.CreateObservableGauge("asr.sensor.absolute_humidity", () => Observe(s => s.AbsoluteHumidity));
        meter.CreateObservableGauge("asr.sensor.dew_point", () => Observe(s => s.DewPoint));
        meter.CreateObservableGauge("asr.sensor.temperature_trend", () => Observe(s => s.TemperatureTrend));
        meter.CreateObservableGauge("asr.sensor.humidity_trend", () => Observe(s => s.HumidityTrend));
        meter.CreateObservableGauge("asr.sensor.temperature_ema", () => Observe(s => s.TemperatureEma));
        meter.CreateObservableGauge("asr.sensor.absolute_humidity_ema", () => Observe(s => s.AbsoluteHumidityEma));
        meter.CreateObservableGauge("asr.sensor.window_open", () => Observe(s => s.WindowOpen ? 1 : 0));
        meter.CreateObservableGauge("asr.sensor.battery_low", () => Observe(s => s.BatteryLow ? 1 : 0));
    }

    private IEnumerable<Measurement<double>> Observe(Func<Snapshot, double> selector)
    {
        foreach (var sensor in sensors)
        {
            yield return new Measurement<double>(selector(sensor.Value), new KeyValuePair<string, object?>("sensor", sensor.Key));
        }
    }

    internal void RecordReading(Sensor sensor)
    {
        sensors[sensor.Name] = new Snapshot(
            sensor.Temperature,
            sensor.Humidity,
            sensor.AbsoluteHumidity,
            sensor.DewPoint,
            sensor.TemperatureTrend,
            sensor.HumidityTrend,
            sensor.TemperatureEmaValue,
            sensor.AbsoluteHumidityEmaValue,
            sensor.WindowOpen,
            sensor.BatteryLow);
        readingsProcessed.Add(1, new KeyValuePair<string, object?>("sensor", sensor.Name));
    }

    public void RecordReadingError() => readingErrors.Add(1);

    public void RecordMqttPublish(double elapsedMs)
    {
        mqttPublished.Add(1);
        mqttPublishDuration.Record(elapsedMs);
    }

    public void RecordMqttPublishError() => mqttPublishErrors.Add(1);

    public void RecordMqttReconnect() => mqttReconnects.Add(1);

    public void SetMqttConnected(bool connected) => mqttConnected = connected ? 1 : 0;

    public void RecordDbOperation(string operation, double elapsedMs)
    {
        var tag = new KeyValuePair<string, object?>("operation", operation);
        dbOperations.Add(1, tag);
        dbOperationDuration.Record(elapsedMs, tag);
    }

    public void RecordDbError(string operation) => dbErrors.Add(1, new KeyValuePair<string, object?>("operation", operation));
}
