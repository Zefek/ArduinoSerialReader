using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TemperatureSensorArduinoReader.Climate;

record ClimateRunOptions(bool DryRun, bool Comments, string? OutputFile);

class ClimateReportRunner
{
    readonly ClimateSettings settings;
    readonly ClimateRepository repository;
    readonly ClimateCommentWriter commentWriter;
    readonly ILogger<ClimateReportRunner> logger;
    readonly ClimatePublisher? publisher;
    readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public ClimateReportRunner(ClimateSettings settings, ClimateRepository repository, ClimateCommentWriter commentWriter, ILogger<ClimateReportRunner> logger, ClimatePublisher? publisher = null)
    {
        this.settings = settings;
        this.repository = repository;
        this.commentWriter = commentWriter;
        this.logger = logger;
        this.publisher = publisher;
    }

    public async Task<bool> Run(IReadOnlyList<DateOnly> days, ClimateRunOptions run, CancellationToken cancellationToken)
    {
        var ok = true;
        foreach (var day in days)
        {
            try
            {
                var builder = new ClimateReportBuilder(settings, repository, day);
                var facts = await builder.Build();
                var last = day == days[^1];
                if (run.Comments && settings.Options.Comment.Enabled && last)
                {
                    builder.Comment = await commentWriter.Write(facts);
                    facts["comment"] = builder.Comment.Text;
                    facts["comment_source"] = builder.Comment.Model;
                    facts["comment_attempts"] = new JsonArray(builder.Comment.Attempts.Select(a => (JsonNode)a).ToArray());
                    logger.LogInformation("Climate comment {Day} from {Model} ({TokensIn}/{TokensOut} tokens): {Comment}", day, builder.Comment.Model, builder.Comment.TokensIn, builder.Comment.TokensOut, builder.Comment.Text);
                    if (builder.Comment.Attempts.Any(a => !a.EndsWith(": ok", StringComparison.Ordinal)))
                    {
                        logger.LogWarning("Climate comment attempts {Day}: {Attempts}", day, string.Join(" | ", builder.Comment.Attempts));
                    }
                }
                if (run.OutputFile != null)
                {
                    await File.WriteAllTextAsync(run.OutputFile, facts.ToJsonString(jsonOptions), cancellationToken);
                }
                if (!run.DryRun)
                {
                    await repository.Save(day, builder);
                    if (last && publisher != null && settings.Options.HomeAssistant.Publish)
                    {
                        await publisher.Publish(builder, cancellationToken);
                    }
                }
                logger.LogInformation("Climate report {Day}: {Status}, {Highlights} highlights, {Metrics} metrics", day, builder.Status, builder.Highlights.Count, builder.Metrics.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ok = false;
                logger.LogError(ex, "Climate report {Day} failed", day);
            }
        }
        return ok;
    }
}
