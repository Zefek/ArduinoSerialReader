// See https://aka.ms/new-console-template for more information
internal class TemperatureAppSettings
{
    public string MqttBroker { get; set; }
    public int MqttPort { get; set; }
    public string MQTTUsername { get; set; }
    public string MQTTPassword { get; set; }
    public string COMPort { get; set; }
}