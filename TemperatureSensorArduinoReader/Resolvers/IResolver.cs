using System.Buffers;

namespace TemperatureSensorArduinoReader.Resolvers;

internal interface IResolver
{
    SensorData Resolve(ReadOnlySequence<byte> payload);
}