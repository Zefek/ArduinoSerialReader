using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TemperatureSensorArduinoReader
{
    internal class Room
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public int? SensorId {  get; set; }
        public int? SensorChannel {  get; set; }
        public string TopicName { get; set; }
    }
}
