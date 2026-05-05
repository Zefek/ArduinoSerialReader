using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TemperatureSensorArduinoReader;
public class HomeAssistantSensor
{
    public static dynamic CreateTemperature(string sensorName)
    {
        return new
        {
            name = "Temperature",
            state_topic = "TX07KTXC/" + sensorName + "/state",
            unit_of_measurement = "°C",
            device_class = "temperature",
            expire_after = 600,
            unique_id = "TX07KTXC_" + sensorName + "_temperature",
            value_template = "{{ value_json.temperature }}",
            device = new
            {
                name = "TX07K-TXC/" + sensorName,
                identifiers = new[] { sensorName },
                via_device = "TX07K-TXC"
            }
        };
    }

    public static dynamic CreateHumidity(string sensorName)
    {
        return new
        {
            name = "Humidity",
            state_topic = "TX07KTXC/" + sensorName + "/state",
            unit_of_measurement = "%",
            device_class = "humidity",
            expire_after = 600,
            unique_id = "TX07KTXC_" + sensorName + "_humidity",
            value_template = "{{ value_json.humidity }}",
            device = new
            {
                name = "TX07K-TXC/" + sensorName,
                identifiers = new[] { sensorName },
                via_device = "TX07K-TXC"
            }
        };
    }

    public static dynamic CreateBattery(string sensorName)
    {
        return new
        {
            name = "Battery",
            state_topic = "TX07KTXC/" + sensorName + "/state",
            expire_after = 600,
            device_class = "battery",
            unique_id = "TX07KTXC_" + sensorName + "_battery",
            value_template = "{{ value_json.battery }}",
            device = new
            {
                name = "TX07K-TXC/" + sensorName,
                identifiers = new[] { sensorName },
                via_device = "TX07K-TXC"
            }
        };
    }
    public static dynamic CreateTrend(string sensorName)
    {
        return new
        {
            name = "Trend",
            state_topic = "TX07KTXC/" + sensorName + "/state",
            expire_after = 600,
            unique_id = "TX07KTXC_" + sensorName + "_trend",
            value_template = "{{ value_json.trend }}",
            device = new
            {
                name = "TX07K-TXC/" + sensorName,
                identifiers = new[] { sensorName },
                via_device = "TX07K-TXC"
            }
        };
    }

    public static dynamic CreateDewPoint(string sensorName)
    {
        return new
        {
            name = "Dew Point",
            state_topic = "TX07KTXC/" + sensorName + "/state",
            unit_of_measurement = "°C",
            device_class = "temperature",
            expire_after = 600,
            unique_id = "TX07KTXC_" + sensorName + "_dew_point",
            value_template = "{{ value_json.dewPoint }}",
            device = new
            {
                name = "TX07K-TXC/" + sensorName,
                identifiers = new[] { sensorName },
                via_device = "TX07K-TXC"
            }
        };
    }

    public static dynamic CreateAbsoluteHumidity(string sensorName)
    {
        return new
        {
            name = "Absolute Humidity",
            state_topic = "TX07KTXC/" + sensorName + "/state",
            unit_of_measurement = "g/m³",
            device_class = "absolute_humidity",
            expire_after = 600,
            unique_id = "TX07KTXC_" + sensorName + "_absolute_humidity",
            value_template = "{{ value_json.absoluteHumidity }}",
            device = new
            {
                name = "TX07K-TXC/" + sensorName,
                identifiers = new[] { sensorName },
                via_device = "TX07K-TXC"
            }
        };
    }

    public static dynamic CreateTemperatureTrend(string sensorName)
    {
        return new
        {
            name = "Temperature Trend",
            state_topic = "TX07KTXC/" + sensorName + "/state",
            state_class = "measurement",
            unit_of_measurement = "°C/h",
            expire_after = 600,
            unique_id = "TX07KTXC_" + sensorName + "_temperature_trend",
            value_template = "{{ value_json.temperatureTrend }}",
            device = new
            {
                name = "TX07K-TXC/" + sensorName,
                identifiers = new[] { sensorName },
                via_device = "TX07K-TXC"
            }
        };
    }

    public static dynamic CreateHumidityTrend(string sensorName)
    {
        return new
        {
            name = "Humidity Trend",
            state_topic = "TX07KTXC/" + sensorName + "/state",
            state_class = "measurement",
            unit_of_measurement = "%/h",
            expire_after = 600,
            unique_id = "TX07KTXC_" + sensorName + "_humidity_trend",
            value_template = "{{ value_json.humidityTrend }}",
            device = new
            {
                name = "TX07K-TXC/" + sensorName,
                identifiers = new[] { sensorName },
                via_device = "TX07K-TXC"
            }
        };
    }

    public static dynamic CreateWindowOpen(string sensorName)
    {
        return new
        {
            name = "Window Open",
            state_topic = "TX07KTXC/" + sensorName + "/state",
            device_class = "window",
            expire_after = 600,
            unique_id = "TX07KTXC_" + sensorName + "_window_open",
            value_template = "{{ value_json.windowOpen }}",
            device = new
            {
                name = "TX07K-TXC/" + sensorName,
                identifiers = new[] { sensorName },
                via_device = "TX07K-TXC"
            }
        };
    }
}
