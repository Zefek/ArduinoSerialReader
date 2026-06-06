using System.Buffers;
using System.Text;

namespace TemperatureSensorArduinoReader.Resolvers;

internal class GarageResolver : IResolver
{
    public SensorData Resolve(ReadOnlySequence<byte> payload)
    {
        var stringPayload = Encoding.UTF8.GetString(payload.ToArray());
        var split = stringPayload.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        return new SensorData
        {
            Id = int.Parse(split[0]),
            Channel = int.Parse(split[3]),
            Humidity = int.Parse(split[2]),
            Temperature = int.Parse(split[1]) / (double)10
        };
    }
}
