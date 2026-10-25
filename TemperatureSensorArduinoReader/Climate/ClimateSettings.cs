namespace TemperatureSensorArduinoReader.Climate;

public class ClimateSettings
{
    public ClimateOptions Options { get; }
    public TimeZoneInfo Zone { get; } = TimeZoneInfo.Local;
    public HashSet<string> NoSurfaceRisk { get; }
    public string[] CommentModels { get; }
    public string PromptFile { get; }

    public ClimateSettings(ClimateOptions options)
    {
        Options = options;
        NoSurfaceRisk = options.Rooms.Where(r => !r.Value.SurfaceRisk).Select(r => r.Key).Append(options.OutsideRoom).ToHashSet();
        CommentModels = options.Comment.Models.Count > 0 ? options.Comment.Models.ToArray() : ["gemma4:31b-cloud", "nemotron-3-ultra:cloud"];
        PromptFile = Path.GetFullPath(options.Comment.Prompt, AppContext.BaseDirectory);
    }

    public string OutsideRoom => Options.OutsideRoom;

    public string OllamaUrl => Options.Comment.OllamaUrl.TrimEnd('/');

    public double DefaultF => Options.DefaultF;

    public double F(string room) => Options.Rooms.TryGetValue(room, out var r) && r.F is double f ? f : Options.DefaultF;

    public double VentilationFloor(string room) => Options.Rooms.TryGetValue(room, out var r) && r.VentilationFloor is double v ? v : Options.VentilationFloor;

    public string DisplayName(string room) => Options.Rooms.TryGetValue(room, out var r) && r.Name is { Length: > 0 } name ? name : room;

    public string RoomType(string room) => Options.Rooms.TryGetValue(room, out var r) && r.Type is { Length: > 0 } type ? type : "místnost";
}
