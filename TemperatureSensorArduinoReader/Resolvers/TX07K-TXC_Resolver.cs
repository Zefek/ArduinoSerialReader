using Microsoft.Extensions.Logging;
using System.Buffers;

namespace TemperatureSensorArduinoReader.Resolvers;

internal class TX07K_TXC_Resolver : IResolver
{
    private const int FrameLength = 5;
    private readonly ILogger<TX07K_TXC_Resolver> logger;

    public TX07K_TXC_Resolver(ILogger<TX07K_TXC_Resolver> logger)
    {
        this.logger = logger;
    }

    public SensorData Resolve(ReadOnlySequence<byte> payload)
    {
        var sensorData = payload.ToArray();
        if (payload.Length != FrameLength)
        {
            logger.LogError("Invalid sensor frame: expected {expected} bytes, got {actual}", FrameLength, payload.Length);
            throw new Exception("Could not resolve sensor data");
        }
        // Split 5 bytes into 10 nibbles (as Arduino TX07K protocol works with nibbles)
        var nibbles = new byte[10];
        for (int i = 0; i < 5; i++)
        {
            nibbles[i * 2] = (byte)((sensorData[i] >> 4) & 0x0F);
            nibbles[i * 2 + 1] = (byte)(sensorData[i] & 0x0F);
        }

        // CRC check: nibble[2] is CRC, swap position 2 with position 9 before checking
        var crc = nibbles[2];
        var toCheck = new byte[10];
        Array.Copy(nibbles, toCheck, 10);
        toCheck[2] = nibbles[9];
        if (!CheckCRC(toCheck, crc))
        {
            throw new Exception("CRC checksum is invalid");
        }

        return new SensorData
        {
            Id = sensorData[0],
            Temperature = ((((sensorData[2] << 4) + ((sensorData[3] & 0xF0) >> 4)) * (double)0.1) - 90 - 32) * ((double)5 / 9),
            Humidity = ((sensorData[3] & 0x0F) * 10) + ((sensorData[4] & 0xF0) >> 4),
            Channel = sensorData[4] & 0x0F,
            BatteryLow = (sensorData[1] & 0x04) != 0,
            TemperatureDown = (sensorData[1] & 0x02) != 0,
            TemperatureUp = (sensorData[1] & 0x01) != 0,
            ForcedTransmition = (sensorData[1] & 0x08) != 0
        };
    }

    private bool CheckCRC(byte[] nibbles, byte crc)
    {
        int rem = 0;
        for (int i = 0; i < 9; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                if ((rem & 0x08) != 0)
                {
                    rem = (rem << 1) ^ 3;
                }
                else
                {
                    rem <<= 1;
                }
            }
            rem ^= nibbles[i];
        }
        return (rem & 0x0F) == crc;
    }
}
