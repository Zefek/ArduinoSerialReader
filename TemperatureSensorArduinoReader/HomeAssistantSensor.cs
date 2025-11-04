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
            state_topic = "TX07KTXC/" + sensorName + "/state",
            unit_of_measurement = "°C",
            device_class = "temperature",
            expire_after = 600,
            unique_id = "TX07KTXC_" + sensorName + "_temperature",
            value_template = "{% set num1 = value[4] | int(base=16,default=0) * 256 %}{% set num2 = value[5] | int(base=16,default=0) * 16 %}{% set num3 = value[6] | int(base=16,default=0) %}{{ ((num1 + num2 + num3)*0.1-122)*(5/9) }}",
            device = new
            {
                name = "TX07K-TXC/" + sensorName,
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
            unique_id = "TX07KTXC_" + sensorName + "_humidity",
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
            unique_id = "TX07KTXC_" + sensorName + "_battery",
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
            unique_id = "TX07KTXC_" + sensorName + "_trend",
            value_template = "{% set up = value[3] | int(base=16,default=0) | bitwise_and(1) | bool %}{% set down = value[3] | int(base=16,default=0) | bitwise_and(2) | bool %}{% if up %}↗{% elif down %}↘{% else %}→{% endif %}",
            device = new
            {
                name = "TX07K-TXC/" + sensorName,
                identifiers = new[] { sensorName }
            }
        };
    }

    public static dynamic CreateWindowState(string sensorName)
    {
        return new
        {
            name = "Window",
            device_class = "window",
            state_topic = "TX07KTXC/" + sensorName + "/state",
            unique_id = "TX07KTXC_" + sensorName + "_window",
            expire_after = 900,
            value_template = @"
{% set t_curr = states('sensor.tx07ktxc_' + '" + sensorName + @"' + '_temperature') | float(0) %}
{% set t_prev = state_attr('binary_sensor.tx07ktxc_' + '" + sensorName + @"' + '_window', 'last_temp') | float(t_curr) %}
{% set drop = t_prev - t_curr %}
{% set h_curr = states('sensor.tx07ktxc_' + '" + sensorName + @"' + '_humidity') | float(0) %}
{% set h_prev = state_attr('binary_sensor.tx07ktxc_' + '" + sensorName + @"' + '_window', 'last_humidity') | float(h_curr) %}
{% set hdrop = h_prev - h_curr %}

{# získej počet poklesů a stav #}
{% set drop_count = state_attr('binary_sensor.tx07ktxc_' + '" + sensorName + @"' + '_window', 'drop_count') | int(0) %}
{% set rise_count = state_attr('binary_sensor.tx07ktxc_' + '" + sensorName + @"' + '_window', 'rise_count') | int(0) %}
{% set is_open = states('binary_sensor.tx07ktxc_' + '" + sensorName + @"' + '_window') == 'on' %}

{% if drop > 0.8 or hdrop > 3 %}
  {% set drop_count = drop_count + 1 %}
  {% set rise_count = 0 %}
{% elif drop < 0.2 and hdrop < 1 %}
  {% set rise_count = rise_count + 1 %}
  {% set drop_count = 0 %}
{% endif %}

{% if not is_open and drop_count >= 3 %}
  {% set is_open = true %}
{% elif is_open and rise_count >= 3 %}
  {% set is_open = false %}
{% endif %}

{{ 'ON' if is_open else 'OFF' }}
",
            json_attributes_template = @"
{
  ""last_temp"": {{ states('sensor.tx07ktxc_' + '" + sensorName + @"' + '_temperature') }},
  ""last_humidity"": {{ states('sensor.tx07ktxc_' + '" + sensorName + @"' + '_humidity') }},
  ""drop_count"": {{ (state_attr('binary_sensor.tx07ktxc_' + '" + sensorName + @"' + '_window', 'drop_count') | int(0)) if (drop_count is not defined) else drop_count }},
  ""rise_count"": {{ (state_attr('binary_sensor.tx07ktxc_' + '" + sensorName + @"' + '_window', 'rise_count') | int(0)) if (rise_count is not defined) else rise_count }}
}
",
            device = new
            {
                name = "TX07K-TXC/" + sensorName,
                identifiers = new[] { sensorName }
            }
        };
    }

}
