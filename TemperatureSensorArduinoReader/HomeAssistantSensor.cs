namespace TemperatureSensorArduinoReader;

public class HomeAssistantSensor
{
    private const string StateTopicPrefix = "TX07KTXC/";
    private const string StateTopicSuffix = "/state";
    private const string UniqueIdPrefix = "TX07KTXC_";
    private const string DeviceNamePrefix = "TX07K-TXC/";
    private const string ViaDevice = "TX07K-TXC";

    private HomeAssistantSensor()
    {
    }

    public static dynamic CreateTemperature(string sensorName)
    {
        return new
        {
            name = "Temperature",
            state_topic = StateTopicPrefix + sensorName + StateTopicSuffix,
            unit_of_measurement = "°C",
            device_class = "temperature",
            expire_after = 600,
            unique_id = UniqueIdPrefix + sensorName + "_temperature",
            value_template = "{{ value_json.temperature }}",
            device = new
            {
                name = DeviceNamePrefix + sensorName,
                identifiers = new[] { sensorName },
                via_device = ViaDevice
            }
        };
    }

    public static dynamic CreateHumidity(string sensorName)
    {
        return new
        {
            name = "Humidity",
            state_topic = StateTopicPrefix + sensorName + StateTopicSuffix,
            unit_of_measurement = "%",
            device_class = "humidity",
            expire_after = 600,
            unique_id = UniqueIdPrefix + sensorName + "_humidity",
            value_template = "{{ value_json.humidity }}",
            device = new
            {
                name = DeviceNamePrefix + sensorName,
                identifiers = new[] { sensorName },
                via_device = ViaDevice
            }
        };
    }

    public static dynamic CreateBattery(string sensorName)
    {
        return new
        {
            name = "Battery",
            state_topic = StateTopicPrefix + sensorName + StateTopicSuffix,
            expire_after = 600,
            device_class = "battery",
            unique_id = UniqueIdPrefix + sensorName + "_battery",
            value_template = "{{ value_json.battery }}",
            device = new
            {
                name = DeviceNamePrefix + sensorName,
                identifiers = new[] { sensorName },
                via_device = ViaDevice
            }
        };
    }

    public static dynamic CreateTrend(string sensorName)
    {
        return new
        {
            name = "Trend",
            state_topic = StateTopicPrefix + sensorName + StateTopicSuffix,
            expire_after = 600,
            unique_id = UniqueIdPrefix + sensorName + "_trend",
            value_template = "{{ value_json.trend }}",
            device = new
            {
                name = DeviceNamePrefix + sensorName,
                identifiers = new[] { sensorName },
                via_device = ViaDevice
            }
        };
    }

    public static dynamic CreateDewPoint(string sensorName)
    {
        return new
        {
            name = "Dew Point",
            state_topic = StateTopicPrefix + sensorName + StateTopicSuffix,
            unit_of_measurement = "°C",
            device_class = "temperature",
            expire_after = 600,
            unique_id = UniqueIdPrefix + sensorName + "_dew_point",
            value_template = "{{ value_json.dewPoint }}",
            device = new
            {
                name = DeviceNamePrefix + sensorName,
                identifiers = new[] { sensorName },
                via_device = ViaDevice
            }
        };
    }

    public static dynamic CreateAbsoluteHumidity(string sensorName)
    {
        return new
        {
            name = "Absolute Humidity",
            state_topic = StateTopicPrefix + sensorName + StateTopicSuffix,
            unit_of_measurement = "g/m³",
            device_class = "absolute_humidity",
            expire_after = 600,
            unique_id = UniqueIdPrefix + sensorName + "_absolute_humidity",
            value_template = "{{ value_json.absoluteHumidity }}",
            device = new
            {
                name = DeviceNamePrefix + sensorName,
                identifiers = new[] { sensorName },
                via_device = ViaDevice
            }
        };
    }

    public static dynamic CreateTemperatureTrend(string sensorName)
    {
        return new
        {
            name = "Temperature Trend",
            state_topic = StateTopicPrefix + sensorName + StateTopicSuffix,
            state_class = "measurement",
            unit_of_measurement = "°C/h",
            expire_after = 600,
            unique_id = UniqueIdPrefix + sensorName + "_temperature_trend",
            value_template = "{{ value_json.temperatureTrend }}",
            device = new
            {
                name = DeviceNamePrefix + sensorName,
                identifiers = new[] { sensorName },
                via_device = ViaDevice
            }
        };
    }

    public static dynamic CreateHumidityTrend(string sensorName)
    {
        return new
        {
            name = "Humidity Trend",
            state_topic = StateTopicPrefix + sensorName + StateTopicSuffix,
            state_class = "measurement",
            unit_of_measurement = "g/m³/h",
            expire_after = 600,
            unique_id = UniqueIdPrefix + sensorName + "_humidity_trend",
            value_template = "{{ value_json.humidityTrend }}",
            device = new
            {
                name = DeviceNamePrefix + sensorName,
                identifiers = new[] { sensorName },
                via_device = ViaDevice
            }
        };
    }

    public static dynamic CreateWindowOpen(string sensorName)
    {
        return new
        {
            name = "Window Open",
            state_topic = StateTopicPrefix + sensorName + StateTopicSuffix,
            device_class = "window",
            expire_after = 600,
            unique_id = UniqueIdPrefix + sensorName + "_window_open",
            value_template = "{{ value_json.windowOpen }}",
            device = new
            {
                name = DeviceNamePrefix + sensorName,
                identifiers = new[] { sensorName },
                via_device = ViaDevice
            }
        };
    }
}
