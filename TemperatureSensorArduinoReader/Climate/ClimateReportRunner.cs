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
                await RunDay(day, day == days[^1], run, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ok = false;
                logger.LogError(ex, "Climate report {Day} failed", day);
            }
        }
        return ok;
    }

    async Task RunDay(DateOnly day, bool last, ClimateRunOptions run, CancellationToken cancellationToken)
    {
        var builder = new ClimateReportBuilder(settings, repository, day);
        var facts = await builder.Build();
        if (run.Comments && settings.Options.Comment.Enabled && last)
        {
            await AddComment(day, builder, facts);
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

    async Task AddComment(DateOnly day, ClimateReportBuilder builder, JsonObject facts)
    {
        var comment = await commentWriter.Write(facts);
        builder.Comment = comment;
        facts["comment"] = comment.Text;
        facts["comment_source"] = comment.Model;
        facts["comment_attempts"] = new JsonArray(comment.Attempts.Select(a => (JsonNode)a).ToArray());
        logger.LogInformation("Climate comment {Day} from {Model} ({TokensIn}/{TokensOut} tokens): {Comment}", day, comment.Model, comment.TokensIn, comment.TokensOut, comment.Text);
        if (comment.Attempts.Any(a => !a.EndsWith(": ok", StringComparison.Ordinal)))
        {
            logger.LogWarning("Climate comment attempts {Day}: {Attempts}", day, string.Join(" | ", comment.Attempts));
        }
    }
}
