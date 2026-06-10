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

        private const double temperatureOpenThreshold = -0.7; // °C/h (pokles)
        private const double temperatureCloseThreshold = -0.1; // °C/h (pokles po otevření)
        private const double humidityOpenThreshold = -1.5; // g/m3/h (pokles)
        private const double humidityCloseThreshold = -0.6; // g/m3/h
        private const int sensorMinReceiveInterval = 30; //30s
        private const int sensorMaxReceiveInterval = 1; //1h
        private const int tauSeconds = 750;

        private double temperatureEma = 0;
        private double absoluteHumidityEma = 0;
        private DateTime lastUpdate = DateTime.MinValue;

        internal double TemperatureEmaValue => temperatureEma;
        internal double AbsoluteHumidityEmaValue => absoluteHumidityEma;
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
            absoluteHumidityEma = state.AbsoluteHumidityEma;
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

            var a = 17.62;
            var b = 243.12;
            var gamma = (a * Temperature) / (b + Temperature) + Math.Log((double)Humidity / 100);
            DewPoint = (b * gamma) / (a - gamma);
            var es = 6.112 * Math.Exp((a * Temperature) / (b + Temperature));

            AbsoluteHumidity = es * Humidity * 2.1674 / (273.15 + Temperature);
            ComputeEma();
        }

        public void Update(SensorData sensorData)
        {
            SetSensorData(sensorData);
        }

        private void ComputeEma()
        {
            var currentDateTime = DateTime.UtcNow;
            if (lastUpdate == DateTime.MinValue)
            {
                temperatureEma = (double)Temperature;
                absoluteHumidityEma = (double)AbsoluteHumidity;
                lastUpdate = currentDateTime;
                return;
            }

            var totalSeconds = (currentDateTime - lastUpdate).TotalSeconds;
            var totalHours = (currentDateTime - lastUpdate).TotalHours;

            if (totalSeconds < sensorMinReceiveInterval)
            {
                return;
            }
            if (totalHours > sensorMaxReceiveInterval)
            {
                temperatureEma = (double)Temperature;
                absoluteHumidityEma = (double)AbsoluteHumidity;
                lastUpdate = currentDateTime;
                return;
            }

            var a = 1.0 - Math.Exp(-totalSeconds / tauSeconds);

            var newTemperatureEma = (a * (double)Temperature) + ((1 - a) * temperatureEma);
            var newAbsoluteHumidityEma = (a * (double)AbsoluteHumidity) + ((1 - a) * absoluteHumidityEma);

            TemperatureTrend = (newTemperatureEma - temperatureEma) / totalHours;
            HumidityTrend = (newAbsoluteHumidityEma - absoluteHumidityEma) / totalHours;

            if (!WindowOpen && TemperatureTrend <= temperatureOpenThreshold && HumidityTrend <= humidityOpenThreshold)
            {
                WindowOpen = true;
            }

            else if (WindowOpen && (TemperatureTrend > temperatureCloseThreshold || HumidityTrend > humidityCloseThreshold))
            {
                WindowOpen = false;
            }

            temperatureEma = newTemperatureEma;
            absoluteHumidityEma = newAbsoluteHumidityEma;
            lastUpdate = currentDateTime;
        }
    }
}
