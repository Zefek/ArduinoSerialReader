using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TemperatureSensorArduinoReader.Climate;

record ClimateComment(string Text, string Model, int? TokensIn, int? TokensOut, List<string> Attempts);

class ClimateCommentWriter
{
    const string TrendKey = "trend";
    const string RoomSingular = "místnosti";
    const string RoomAccusative = "místnost";
    const string RoomLocativePlural = "místnostech";
    const string RoomGenitivePlural = "místností";
    const string RoomDativePlural = "místnostem";

    readonly ClimateSettings settings;
    readonly HttpClient http;
    readonly TimeSpan regexTimeout = TimeSpan.FromSeconds(1);
    readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    readonly Dictionary<string, string> roomTrendNames = new()
    {
        [ClimateReportBuilder.SurfaceHours80] = "riziko_plisne_hodin",
        [ClimateReportBuilder.RhAvg] = "vlhkost_prumer_pct",
        [ClimateReportBuilder.AhExcess] = "vlhkost_nad_venkovni_g_m3",
        [ClimateReportBuilder.VentEfficiency] = "ucinnost_vetrani_pct",
        [ClimateReportBuilder.TAvg] = "teplota_prumer",
        [ClimateReportBuilder.NightRate] = "nocni_chladnuti_c_za_h",
    };

    readonly Dictionary<string, string> outsideTrendNames = new()
    {
        [ClimateReportBuilder.TAvg] = "teplota_prumer",
        [ClimateReportBuilder.AhAvg] = "absolutni_g_m3",
    };

    public ClimateCommentWriter(ClimateSettings settings)
    {
        this.settings = settings;
        http = new HttpClient { Timeout = TimeSpan.FromSeconds(settings.Options.Comment.TimeoutSeconds) };
    }

    JsonObject? TranslateTrend(JsonNode? trend, Dictionary<string, string> trendNames)
    {
        if (trend is not JsonObject source)
        {
            return null;
        }
        var result = new JsonObject();
        foreach (var (metric, value) in source)
        {
            if (!trendNames.TryGetValue(metric, out var name) || value is not JsonObject values)
            {
                continue;
            }
            var item = new JsonObject();
            foreach (var (key, czech) in new[] { ("today", "dnes"), ("yesterday", "vcera"), ("avg7", "prumer_7_dni"), ("delta7", "zmena_proti_prumeru") })
            {
                if (values[key] is JsonNode node)
                {
                    item[czech] = node.DeepClone();
                }
            }
            result[name] = item;
        }
        return result.Count > 0 ? result : null;
    }

    string DisplayName(string room) => settings.DisplayName(room);

    string Normalize(string text) =>
        new string(text.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();

    public async Task<ClimateComment> Write(JsonObject facts)
    {
        var rooms = facts["rooms"]!.AsArray().Select(r => r!["name"]!.GetValue<string>()).ToList();
        var codes = rooms.Select((room, i) => (Room: room, Code: $"M{i + 1}")).ToDictionary(x => x.Room, x => x.Code);
        var attempts = new List<string>();
        if (!File.Exists(settings.PromptFile))
        {
            attempts.Add($"prompt {settings.PromptFile} nenalezen");
            return Template(facts, attempts);
        }
        var system = await File.ReadAllTextAsync(settings.PromptFile);
        var input = Condense(facts, codes).ToJsonString(jsonOptions);
        foreach (var model in settings.CommentModels)
        {
            try
            {
                var (text, tokensIn, tokensOut) = await Ask(model, system, input);
                var problem = Validate(text, codes);
                if (problem != null)
                {
                    attempts.Add($"{model}: {problem}");
                    continue;
                }
                attempts.Add($"{model}: ok");
                return new ClimateComment(Restore(text, codes), model, tokensIn, tokensOut, attempts);
            }
            catch (Exception ex)
            {
                attempts.Add($"{model}: {ex.GetType().Name} {ex.Message}");
            }
        }
        return Template(facts, attempts);
    }

    async Task<(string Text, int? TokensIn, int? TokensOut)> Ask(string model, string system, string input)
    {
        var body = new JsonObject
        {
            ["model"] = model,
            ["stream"] = false,
            ["think"] = false,
            ["options"] = new JsonObject { ["num_ctx"] = 8192, ["temperature"] = 0.3 },
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = system },
                new JsonObject { ["role"] = "user", ["content"] = input }),
        };
        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await http.PostAsync($"{settings.OllamaUrl}/api/chat", content);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {json}");
        }
        var result = JsonNode.Parse(json)!;
        var text = result["message"]?["content"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(text) && result["done_reason"]?.GetValue<string>() == "stop")
        {
            text = result["message"]?["thinking"]?.GetValue<string>() ?? "";
        }
        return (text.Trim(), result["prompt_eval_count"]?.GetValue<int>(), result["eval_count"]?.GetValue<int>());
    }

    string? Validate(string text, Dictionary<string, string> codes)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "prazdna odpoved";
        }
        if (text.Length > 3000)
        {
            return "prilis dlouha odpoved";
        }
        if (!Regex.IsMatch(text, @"\*\*[^*\r\n]+:\*\*", RegexOptions.None, regexTimeout))
        {
            return "chybi format **kod:**";
        }
        var known = codes.Values.ToHashSet();
        var unknown = Regex.Matches(text, @"\bM\d+\b", RegexOptions.None, regexTimeout).Select(m => m.Value).Where(c => !known.Contains(c)).Distinct().ToList();
        if (unknown.Count > 0)
        {
            return $"nezname kody {string.Join(", ", unknown)}";
        }
        var normalized = Normalize(text);
        foreach (var room in codes.Keys)
        {
            var stem = Normalize(DisplayName(room));
            stem = stem.Length > 5 ? stem[..5] : stem;
            if (Regex.IsMatch(normalized, $@"\b{Regex.Escape(stem)}", RegexOptions.None, regexTimeout))
            {
                return $"obsahuje nazev mistnosti ({DisplayName(room)})";
            }
        }
        return null;
    }

    readonly Dictionary<string, (string Singular, string Plural)> prepositionForms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["v"] = (RoomSingular, RoomLocativePlural),
        ["ve"] = (RoomSingular, RoomLocativePlural),
        ["na"] = (RoomSingular, RoomLocativePlural),
        ["o"] = (RoomSingular, RoomLocativePlural),
        ["u"] = (RoomSingular, RoomGenitivePlural),
        ["z"] = (RoomSingular, RoomGenitivePlural),
        ["ze"] = (RoomSingular, RoomGenitivePlural),
        ["do"] = (RoomSingular, RoomGenitivePlural),
        ["od"] = (RoomSingular, RoomGenitivePlural),
        ["k"] = (RoomSingular, RoomDativePlural),
        ["ke"] = (RoomSingular, RoomDativePlural),
        ["pro"] = (RoomAccusative, RoomSingular),
        ["za"] = (RoomAccusative, RoomSingular),
    };

    string Restore(string text, Dictionary<string, string> codes)
    {
        text = Regex.Replace(text, @"\b(?<prep>[A-Za-z]{1,3})\s+(?<codes>M\d+(?:\s*(?:,|\ba\b)\s*M\d+)*)\b", m =>
        {
            if (!prepositionForms.TryGetValue(m.Groups["prep"].Value, out var forms))
            {
                return m.Value;
            }
            var plural = Regex.Matches(m.Groups["codes"].Value, @"M\d+", RegexOptions.None, regexTimeout).Count > 1;
            return $"{m.Groups["prep"].Value} {(plural ? forms.Plural : forms.Singular)} {m.Groups["codes"].Value}";
        }, RegexOptions.None, regexTimeout);
        foreach (var (room, code) in codes.OrderByDescending(c => c.Value.Length))
        {
            text = Regex.Replace(text, $@"\b{Regex.Escape(code)}\b", DisplayName(room), RegexOptions.None, regexTimeout);
        }
        return text;
    }

    ClimateComment Template(JsonObject facts, List<string> attempts)
    {
        var lines = facts["highlights"]!.AsArray()
            .Select(h => $"**{DisplayName(h!["room"]!.GetValue<string>())}:** {h["text"]!.GetValue<string>()}")
            .ToList();
        lines.Add(lines.Count == 0 ? "**Dům:** Bez upozornění." : "**Ostatní místnosti:** Bez upozornění.");
        return new ClimateComment(string.Join("\n", lines), "sablona", null, null, attempts);
    }

    JsonObject Condense(JsonObject facts, Dictionary<string, string> codes)
    {
        JsonNode? C(JsonNode? node) => node?.DeepClone();
        var rooms = new JsonArray();
        foreach (var r in facts["rooms"]!.AsArray())
        {
            var name = r!["name"]!.GetValue<string>();
            var t = r["temperature"];
            var h = r["humidity"];
            var s = r["surface"];
            var v = r["ventilation"];
            var n = r["night_cooling"];
            var room = new JsonObject
            {
                ["id"] = codes[name],
                ["typ"] = settings.RoomType(name),
                ["okno"] = r["has_window"]?.GetValue<bool>() ?? true,
                ["stav"] = C(r["status"]),
                ["teplota"] = new JsonObject { ["prumer"] = C(t?["avg"]), ["min"] = C(t?["min"]), ["max"] = C(t?["max"]) },
                ["vlhkost"] = new JsonObject
                {
                    ["prumer_pct"] = C(h?["avg"]),
                    ["max_pct"] = C(h?["max"]),
                    ["hodin_nad_60"] = C(h?["hours_60"]),
                    ["hodin_nad_70"] = C(h?["hours_70"]),
                    ["hodin_pod_35"] = C(h?["hours_below_35"]),
                    ["absolutni_g_m3"] = C(h?[ClimateReportBuilder.AhAvg]),
                    ["nad_venkovni_g_m3"] = C(h?[ClimateReportBuilder.AhExcess]),
                },
            };
            if (s != null)
            {
                room["u_studene_steny"] = new JsonObject
                {
                    ["vlhkost_max_pct"] = C(s["rh_max"]),
                    ["v_hodinu"] = C(s["rh_max_at"]),
                    ["hodin_riziko_plisne"] = C(s["hours_80"]),
                    ["hodin_kondenzace"] = C(s["hours_100"]),
                };
            }
            if (v != null)
            {
                room["vetrani"] = new JsonObject
                {
                    ["pocet"] = C(v["events"]),
                    ["minut"] = C(v["open_minutes"]),
                    ["pokles_vlhkosti_pct"] = C(v["avg_ah_drop_pct"]),
                    ["mozny_pokles_pct"] = C(v["avg_ah_drop_possible_pct"]),
                    ["ucinnost_pct"] = C(v["avg_efficiency_pct"]),
                    ["navrat_za_min"] = C(v["avg_recovery_min"]),
                };
            }
            if (n != null)
            {
                room["nocni_chladnuti_c_za_h"] = C(n["rate_c_per_h"]);
            }
            if (r["battery_low"]?.GetValue<bool>() == true)
            {
                room["slaba_baterie"] = true;
            }
            if (r["coverage_pct"]?.GetValue<double>() is double coverage && coverage < 90)
            {
                room["pokryti_dat_pct"] = coverage;
            }
            if (TranslateTrend(r[TrendKey], roomTrendNames) is JsonObject trend)
            {
                room[TrendKey] = trend;
            }
            rooms.Add(room);
        }
        var o = facts["outside"];
        return new JsonObject
        {
            ["den"] = C(facts["day"]),
            ["venku"] = o == null ? null : new JsonObject
            {
                ["teplota_prumer"] = C(o[ClimateReportBuilder.TAvg]),
                ["teplota_min"] = C(o["t_min"]),
                ["teplota_max"] = C(o["t_max"]),
                ["vlhkost_prumer_pct"] = C(o["rh_avg"]),
                ["absolutni_g_m3"] = C(o[ClimateReportBuilder.AhAvg]),
                [TrendKey] = TranslateTrend(o[TrendKey], outsideTrendNames),
            },
            ["upozorneni"] = new JsonArray(facts["highlights"]!.AsArray()
                .Where(x => codes.ContainsKey(x!["room"]!.GetValue<string>()))
                .Select(x => (JsonNode)new JsonObject { ["uroven"] = C(x!["level"]), ["mistnost"] = codes[x["room"]!.GetValue<string>()], ["text"] = C(x["text"]) })
                .ToArray()),
            ["mistnosti"] = rooms,
        };
    }
}
