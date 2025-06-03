using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TemperatureSensorArduinoReader
{
    internal class Sensor
    {
        internal int Id { get; }
        internal int Humidity { get; }
        internal int Channel { get; }
        public bool BatteryLow { get; }
        public bool TemperatureDown { get; }
        public bool TemperatureUp { get; }
        public bool ForcedTransmition { get; }
        internal decimal Temperature { get; }

        public byte[] Data { get; }


        //426
        // 25944CE761
        //0010 0101 1001 0100 0100 1100 1110 0111 0110 0001
        internal Sensor(SensorData sensorData)
        {
            if (sensorData.Data.Any())
            {
                Id = sensorData.Data[0];
                Temperature = ((((sensorData.Data[2] << 4) + ((sensorData.Data[3] & 0xF0) >> 4))*(decimal)0.1)-90 - 32) * ((decimal)5/9);
                Humidity = ((sensorData.Data[3]&0x0F)*10) + ((sensorData.Data[4]&0xF0) >> 4);
                Channel = sensorData.Data[4]&0x0F;
                BatteryLow = (sensorData.Data[1] & 0x04) != 0;
                TemperatureDown = (sensorData.Data[1] & 0x02) != 0;
                TemperatureUp = (sensorData.Data[1] & 0x01) != 0;
                ForcedTransmition = (sensorData.Data[1] & 0x08) != 0;
                Data = sensorData.Data.ToArray();
            }
            else
            {
                if (sensorData.Payload.Length!=5)
                {
                    throw new Exception("Invalid payload length");
                }
                byte[] bytes = new byte[sensorData.Payload.Length];
                for (int i = 0; i < sensorData.Payload.Length; i++)
                {
                    bytes[i] = (byte)int.Parse(sensorData.Payload[i].ToString(), System.Globalization.NumberStyles.HexNumber);
                }
                Id = bytes[0];
                Temperature = (((bytes[2] + bytes[3] & 0xF0)*(decimal)0.1)-90 - 32) * ((decimal)5/9);
                Humidity = (bytes[3]&0x0F*10) + bytes[4]&0xF0;
                Channel = bytes[4]&0x0F;
                BatteryLow = (bytes[1] & 0x04) != 0;
                TemperatureDown = (bytes[1] & 0x02) != 0;
                TemperatureUp = (bytes[1] & 0x01) != 0;
                ForcedTransmition = (bytes[1] & 0x08) != 0;
                byte[] toCheckCRC = new byte[sensorData.Payload.Length];
                for (int i = 0; i < sensorData.Payload.Length; i++)
                {
                    toCheckCRC[i] = bytes[i];
                }
                var third = bytes[1] & 0xF0;
                var last = bytes[4] & 0x0F;
                toCheckCRC[1] = (byte)(bytes[4]&0x0F + bytes[1]& 0x0F);
                toCheckCRC[4] = (byte)(bytes[1]&0xF0 + bytes[4]&0xF0);
                if (!CheckCRC(toCheckCRC, bytes[2]))
                {
                    throw new Exception("CRC checksum is invalid");
                }
            }
        }

        private bool CheckCRC(byte[] bytes, byte crc)
        {
            int rem = 0;
            for (int i = 0; i < bytes.Length-1; i++)
            {
                for (int j = 0; j <4; j++)
                {
                    if ((rem & 0x08) != 0)
                    {
                        rem = (rem << 1)^3;
                    }
                    else
                    {
                        rem <<= 1;
                    }
                }
                rem ^= bytes[i];
            }
            var result = rem & 0x0F;
            return result == crc;
        }
    }
}
