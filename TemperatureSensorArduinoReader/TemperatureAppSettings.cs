public class TemperatureAppSettings
{
    public string MqttBroker { get; set; }
    public int MqttPort { get; set; }
    public string MQTTUsername { get; set; }
    public string MQTTPassword { get; set; }
    public string COMPort { get; set; }
    public string HomeAssistantWebSocket { get; set; }
    public string HomeAssistantToken { get; set; }
    public string ConnectionString { get; set; }
    public string LokiUrl { get; set; }
}