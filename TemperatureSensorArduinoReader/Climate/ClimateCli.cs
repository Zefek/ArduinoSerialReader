using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace TemperatureSensorArduinoReader.Climate;

class ClimateCli
{
    public async Task<int> Run(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var yesterday = DateOnly.FromDateTime(DateTime.Now).AddDays(-1);
        DateOnly? day = null;
        var backfill = 0;
        var dryRun = false;
        var comments = true;
        string? output = null;
        string? factsFile = null;
        var queue = new Queue<string>(args);
        while (queue.TryDequeue(out var arg))
        {
            switch (arg)
            {
                case "--day":
                    day = DateOnly.ParseExact(queue.Dequeue(), "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    break;
                case "--backfill":
                    backfill = int.Parse(queue.Dequeue(), CultureInfo.InvariantCulture);
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--out":
                    output = queue.Dequeue();
                    break;
                case "--no-comment":
                    comments = false;
                    break;
                case "--facts":
                    factsFile = queue.Dequeue();
                    break;
                default:
                    await Console.Error.WriteLineAsync($"Neznamy parametr {arg}. Pouziti: climate [--day yyyy-MM-dd] [--backfill N] [--dry-run] [--out soubor.json] [--no-comment] [--facts fakta.json]");
                    return 2;
            }
        }

        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
        var options = config.GetSection("Climate").Get<ClimateOptions>() ?? new ClimateOptions();
        var settings = new ClimateSettings(options);
        var commentWriter = new ClimateCommentWriter(settings);

        if (factsFile != null)
        {
            var facts = JsonNode.Parse(await File.ReadAllTextAsync(factsFile))!.AsObject();
            var comment = await commentWriter.Write(facts);
            await Console.Out.WriteLineAsync(comment.Text);
            await Console.Error.WriteLineAsync($"--- {comment.Model}, {comment.TokensIn}/{comment.TokensOut} tokenu; pokusy: {string.Join(" | ", comment.Attempts)}");
            return 0;
        }
        var days = new List<DateOnly>();
        if (backfill > 0)
        {
            for (var d = backfill - 1; d >= 0; d--)
            {
                days.Add(yesterday.AddDays(-d));
            }
        }
        else
        {
            days.Add(day ?? yesterday);
        }

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(config.GetSection("TemperatureAppSettings").GetValue<string>("ConnectionString"))
            .Options;
        await using var db = new AppDbContext(dbOptions);
        var repository = new ClimateRepository(db);
        if (!dryRun)
        {
            await db.Database.MigrateAsync();
        }
        else if ((await db.Database.GetPendingMigrationsAsync()).Any())
        {
            repository.UseHistory = false;
            await Console.Error.WriteLineAsync("Databaze nema vsechny migrace, trend se nepocita.");
        }
        using var loggerFactory = LoggerFactory.Create(b => b.AddSimpleConsole(o => o.SingleLine = true));
        var runner = new ClimateReportRunner(settings, repository, commentWriter, loggerFactory.CreateLogger<ClimateReportRunner>());
        return await runner.Run(days, new ClimateRunOptions(dryRun, comments, output), CancellationToken.None) ? 0 : 1;
    }
}
