public class TemperatureAppSettings
{
    public required string MqttBroker { get; set; }
    public int MqttPort { get; set; }
    public required string MQTTUsername { get; set; }
    public required string MQTTPassword { get; set; }
    public required string COMPort { get; set; }
    public required string HomeAssistantWebSocket { get; set; }
    public required string HomeAssistantToken { get; set; }
    public required string ConnectionString { get; set; }
    public required string LokiUrl { get; set; }
}