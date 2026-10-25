namespace TemperatureSensorArduinoReader.Climate;

public class ClimateOptions
{
    public bool Enabled { get; set; }
    public string RunAt { get; set; } = "06:00";
    public bool RunOnStartup { get; set; }
    public string OutsideRoom { get; set; } = "outside";
    public double DefaultF { get; set; } = 0.74;
    public double VentilationFloor { get; set; } = 3.0;
    public Dictionary<string, ClimateRoomOptions> Rooms { get; set; } = new();
    public ClimateCommentOptions Comment { get; set; } = new();
    public ClimateHomeAssistantOptions HomeAssistant { get; set; } = new();
}

public class ClimateRoomOptions
{
    public string? Name { get; set; }
    public string? Type { get; set; }
    public double? F { get; set; }
    public bool SurfaceRisk { get; set; } = true;
    public double? VentilationFloor { get; set; }
}

public class ClimateCommentOptions
{
    public bool Enabled { get; set; } = true;
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public List<string> Models { get; set; } = new();
    public string Prompt { get; set; } = Path.Combine("Climate", "prompt", "system-prompt.md");
    public int TimeoutSeconds { get; set; } = 180;
}

public class ClimateHomeAssistantOptions
{
    public bool Publish { get; set; } = true;
    public string ObjectId { get; set; } = "klima_report";
    public string Name { get; set; } = "Klima report";
}
