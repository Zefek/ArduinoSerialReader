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
        internal int Id { get; set; }
        public int Humidity { get; private set; }
        internal int Channel { get; set; }
        public bool BatteryLow { get; private set; }
        public bool TemperatureDown { get; private set; }
        public bool TemperatureUp { get; private set; }
        public bool ForcedTransmition { get; private set; }
        public double Temperature { get; private set; }
        public double AbsoluteHumidity { get; private set; }
        public double DewPoint { get; private set; }
        internal bool WindowOpen { get; private set; }
        internal double TemperatureTrend { get; private set; }
        internal double HumidityTrend { get; private set; }

        public byte[] Data { get; set; }

        internal string Name => Id.ToString() + "_" + Channel.ToString();

        private const double alpha = 0.182;
        private const double temperatureOpenThreshold = -0.7; // °C/min (pokles)
        private const double temperatureCloseThreshold = -0.1; // °C/min (pokles po otevření)
        private const double humidityOpenThreshold = -12; // %RH/min (pokles)
        private const double humidityCloseThreshold = -5; // %RH/min

        private double temperatureEma = 0;
        private double humidityEma = 0;
        private DateTime lastUpdate = DateTime.MinValue;

        internal double TemperatureEmaValue => temperatureEma;
        internal double HumidityEmaValue => humidityEma;
        internal DateTime LastUpdateUtc => lastUpdate;


        //426
        // 25944CE761
        //0010 0101 1001 0100 0100 1100 1110 0111 0110 0001
        internal Sensor(SensorData sensorData)
        {
            SetSensorData(sensorData);
        }

        internal Sensor(SensorState state)
        {
            Id = state.SensorId;
            Channel = state.Channel;
            temperatureEma = state.TemperatureEma;
            humidityEma = state.HumidityEma;
            lastUpdate = state.LastUpdate;
            WindowOpen = state.WindowOpen;
        }

        private void SetSensorData(SensorData sensorData)
        {
            if (sensorData.Data.Any())
            {
                // Split 5 bytes into 10 nibbles (as Arduino TX07K protocol works with nibbles)
                var nibbles = new byte[10];
                for (int i = 0; i < 5; i++)
                {
                    nibbles[i * 2] = (byte)((sensorData.Data[i] >> 4) & 0x0F);
                    nibbles[i * 2 + 1] = (byte)(sensorData.Data[i] & 0x0F);
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

                Id = sensorData.Data[0];
                Temperature = ((((sensorData.Data[2] << 4) + ((sensorData.Data[3] & 0xF0) >> 4)) * (double)0.1) - 90 - 32) * ((double)5 / 9);
                Humidity = ((sensorData.Data[3] & 0x0F) * 10) + ((sensorData.Data[4] & 0xF0) >> 4);
                Channel = sensorData.Data[4] & 0x0F;
                BatteryLow = (sensorData.Data[1] & 0x04) != 0;
                TemperatureDown = (sensorData.Data[1] & 0x02) != 0;
                TemperatureUp = (sensorData.Data[1] & 0x01) != 0;
                ForcedTransmition = (sensorData.Data[1] & 0x08) != 0;
                Data = sensorData.Data.ToArray();
            }
            else
            {
                if (sensorData.Payload.Length != 5)
                {
                    throw new Exception("Invalid payload length");
                }
                byte[] bytes = new byte[sensorData.Payload.Length];
                for (int i = 0; i < sensorData.Payload.Length; i++)
                {
                    bytes[i] = (byte)int.Parse(sensorData.Payload[i].ToString(), System.Globalization.NumberStyles.HexNumber);
                }
                Id = bytes[0];
                Temperature = (((bytes[2] + bytes[3] & 0xF0) * (double)0.1) - 90 - 32) * ((double)5 / 9);
                Humidity = (bytes[3] & 0x0F * 10) + bytes[4] & 0xF0;
                Channel = bytes[4] & 0x0F;
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
                toCheckCRC[1] = (byte)(bytes[4] & 0x0F + bytes[1] & 0x0F);
                toCheckCRC[4] = (byte)(bytes[1] & 0xF0 + bytes[4] & 0xF0);
                if (!CheckCRC(toCheckCRC, bytes[2]))
                {
                    throw new Exception("CRC checksum is invalid");
                }
            }
            ComputeEma();
            var a = 17.62;
            var b = 243.12;
            var gamma = (a * Temperature) / (b + Temperature) + Math.Log((double)Humidity / 100);
            DewPoint = (b * gamma) / (a - gamma);
            var es = 6.112 * Math.Exp((a * Temperature) / (b + Temperature));

            AbsoluteHumidity = es * Humidity * 2.1674 / (273.15 + Temperature);
        }

        public void Update(SensorData sensorData)
        {
            SetSensorData(sensorData);
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

        private void ComputeEma()
        {
            if (lastUpdate == DateTime.MinValue)
            {
                temperatureEma = (double)Temperature;
                humidityEma = (double)Humidity;
                lastUpdate = DateTime.UtcNow;
                return;
            }

            var newTemperatureEma = (alpha * (double)Temperature) + ((1 - alpha) * temperatureEma);
            var newHumidityEma = (alpha * (double)Humidity) + ((1 - alpha) * humidityEma);

            var currentDateTime = DateTime.UtcNow;
            TemperatureTrend = (newTemperatureEma - temperatureEma) / (currentDateTime - lastUpdate).TotalHours;
            HumidityTrend = (newHumidityEma - humidityEma) / (currentDateTime - lastUpdate).TotalHours;

            if (!WindowOpen && TemperatureTrend <= temperatureOpenThreshold && HumidityTrend <= humidityOpenThreshold)
            {
                WindowOpen = true;
            }

            else if (WindowOpen && (TemperatureTrend > temperatureCloseThreshold || HumidityTrend > humidityCloseThreshold))
            {
                WindowOpen = false;
            }

            temperatureEma = newTemperatureEma;
            humidityEma = newHumidityEma;
            lastUpdate = currentDateTime;
        }
    }
}
