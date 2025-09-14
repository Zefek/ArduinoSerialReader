using System;
using System.Collections.Generic;
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
            state_topic = "TX07KTXC/" + sensorName+"/state",
            unit_of_measurement = "°C",
            device_class = "temperature",
            expire_after = 600,
            unique_id = Guid.NewGuid().ToString(),
            value_template = "{% set num1 = value[4] | int(base=16,default=0) * 256 %}{% set num2 = value[5] | int(base=16,default=0) * 16 %}{% set num3 = value[6] | int(base=16,default=0) %}{{ ((num1 + num2 + num3)*0.1-122)*(5/9) }}",
            device = new
            {
                name = "TX07K-TXC/"+sensorName,
                identifiers = new[] { sensorName }
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
            unique_id = Guid.NewGuid().ToString(),
            value_template = "{% set num1 = value[7] | int(base=16,default=0) %}{% set num2 = value[8] | int(base=16,default=0) %}{{ num1 * 10 + num2 }}",
            device = new
            {
                name = "TX07K-TXC/" + sensorName,
                identifiers = new[] { sensorName }
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
            unique_id = Guid.NewGuid().ToString(),
            value_template = "{% if value[3] | int(base=16,default=0) | bitwise_and(4) | bool %} Vybitá{% else %}OK{% endif %}",
            device = new
            {
                name = "TX07K-TXC/" + sensorName,
                identifiers = new[] { sensorName }
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
            unique_id = Guid.NewGuid().ToString(),
            value_template = "{% set up = value[3] | int(base=16,default=0) | bitwise_and(1) | bool %}{% set down = value[3] | int(base=16,default=0) | bitwise_and(2) | bool %}{% if up %}↗{% elif down %}↘{% else %}→{% endif %}",
            device = new
            {
                name = "TX07K-TXC/" + sensorName,
                identifiers = new[] { sensorName }
            }
        };
    }
}
