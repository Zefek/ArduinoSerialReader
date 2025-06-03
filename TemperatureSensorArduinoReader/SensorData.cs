using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TemperatureSensorArduinoReader
{
    internal class SensorData
    {
        public string Payload {  get; set; }

        public byte[] Data { get; set; }
    }
}
