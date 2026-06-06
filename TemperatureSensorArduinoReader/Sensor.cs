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
            Id = sensorData.Id;
            Temperature = sensorData.Temperature;
            Humidity = sensorData.Humidity;
            Channel = sensorData.Channel;
            BatteryLow = sensorData.BatteryLow;
            TemperatureDown = sensorData.TemperatureDown;
            TemperatureUp = sensorData.TemperatureUp;
            ForcedTransmition = sensorData.ForcedTransmition;

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
