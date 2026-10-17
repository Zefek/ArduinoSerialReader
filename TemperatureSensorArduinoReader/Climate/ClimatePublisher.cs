using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TemperatureSensorArduinoReader.Climate;

class ClimatePublisher
{
    readonly RabbitService rabbitService;
    readonly ClimateSettings settings;
    readonly ILogger<ClimatePublisher> logger;
    readonly JsonSerializerOptions jsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public ClimatePublisher(RabbitService rabbitService, ClimateSettings settings, ILogger<ClimatePublisher> logger)
    {
        this.rabbitService = rabbitService;
        this.settings = settings;
        this.logger = logger;
    }

    string BaseTopic => $"TX07KTXC/{settings.Options.HomeAssistant.ObjectId}";

    public async Task Publish(ClimateReportBuilder builder, CancellationToken cancellationToken)
    {
        var ha = settings.Options.HomeAssistant;
        var discovery = new JsonObject
        {
            ["name"] = ha.Name,
            ["unique_id"] = ha.ObjectId,
            ["object_id"] = ha.ObjectId,
            ["state_topic"] = $"{BaseTopic}/state",
            ["json_attributes_topic"] = $"{BaseTopic}/attributes",
            ["icon"] = "mdi:home-thermometer-outline",
            ["device"] = new JsonObject
            {
                ["identifiers"] = new JsonArray("climate_report"),
                ["name"] = "Klima domu",
                ["manufacturer"] = "TemperatureSensorArduinoReader",
            },
        };
        var attributes = new JsonObject
        {
            ["day"] = builder.Facts?["day"]?.DeepClone(),
            ["generated_at"] = builder.Facts?["generated_at"]?.DeepClone(),
            ["comment"] = builder.Comment?.Text,
            ["comment_model"] = builder.Comment?.Model,
            ["highlights"] = new JsonArray(builder.Highlights
                .Select(h => (JsonNode)new JsonObject { ["level"] = h.Level, ["room"] = settings.DisplayName(h.Room), ["text"] = h.Text })
                .ToArray()),
        };
        await rabbitService.PublishRetained(discovery.ToJsonString(jsonOptions), $"homeassistant/sensor/{ha.ObjectId}/config", cancellationToken);
        await rabbitService.PublishRetained(attributes.ToJsonString(jsonOptions), $"{BaseTopic}/attributes", cancellationToken);
        await rabbitService.PublishRetained(builder.Status, $"{BaseTopic}/state", cancellationToken);
        logger.LogInformation("Climate report {Day} published to Home Assistant as {ObjectId}: {Status}", builder.Facts?["day"], ha.ObjectId, builder.Status);
    }
}
