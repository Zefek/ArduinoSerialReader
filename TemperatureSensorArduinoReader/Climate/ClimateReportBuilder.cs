using System.Globalization;
using System.Text.Json.Nodes;

namespace TemperatureSensorArduinoReader.Climate;

record ClimateReading(string Room, DateTimeOffset Ts, double Temperature, int Humidity, double AbsoluteHumidity, bool WindowOpen, bool BatteryLow);
record ClimateMetric(string Room, string Name, double Value);
record ClimateHighlight(string Level, string Room, string Text);
record ClimateVentilation(double Drop, double? Potential, double? Efficiency, double? Recovery, JsonObject Detail);

class ClimateReportBuilder
{
    public const string TAvg = "t_avg";
    public const string TMin = "t_min";
    public const string AhAvg = "ah_avg";
    public const string AhExcess = "ah_excess";
    public const string RhAvg = "rh_avg";
    public const string SurfaceHours80 = "surface_hours_80";
    public const string VentEfficiency = "vent_efficiency_pct";
    public const string NightRate = "night_rate";

    const string Fail = "fail";
    const string Warn = "warn";
    const string Info = "info";
    const string Ok = "ok";

    readonly ClimateSettings settings;
    readonly ClimateRepository repository;
    readonly DateOnly day;
    readonly DateTimeOffset start;
    readonly DateTimeOffset end;
    Dictionary<(string Room, string Name), double> history = new();
    Dictionary<(string Room, string Name), double> yesterday = new();
    List<ClimateReading> outsideAll = new();
    List<ClimateReading> outsideDay = new();
    List<ClimateReading> outsideNight = new();
    Dictionary<int, (double T, double Ah, double Rh)> outsideHourly = new();

    public List<ClimateMetric> Metrics { get; } = new();
    public List<ClimateHighlight> Highlights { get; } = new();
    public JsonObject? Facts { get; private set; }
    public string Status { get; private set; } = Ok;
    public ClimateComment? Comment { get; set; }

    public ClimateReportBuilder(ClimateSettings settings, ClimateRepository repository, DateOnly day)
    {
        this.settings = settings;
        this.repository = repository;
        this.day = day;
        start = LocalMidnight(day);
        end = LocalMidnight(day.AddDays(1));
    }

    DateTimeOffset LocalMidnight(DateOnly d)
    {
        var local = d.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, settings.Zone.GetUtcOffset(local));
    }

    string Local(DateTimeOffset t, string format = "HH:mm") => TimeZoneInfo.ConvertTime(t, settings.Zone).ToString(format, CultureInfo.InvariantCulture);
    int LocalHour(DateTimeOffset t) => TimeZoneInfo.ConvertTime(t, settings.Zone).Hour;
    double R(double v, int digits = 1) => Math.Round(v, digits);

    void Add(string room, string name, double value) => Metrics.Add(new ClimateMetric(room, name, value));
    void Highlight(string level, string room, string text) => Highlights.Add(new ClimateHighlight(level, room, text));
    double? Avg7(string room, string name) => history.TryGetValue((room, name), out var v) ? v : null;

    double SaturationDensity(double t) => 216.7 * (6.112 * Math.Exp(17.62 * t / (243.12 + t))) / (273.15 + t);

    Dictionary<int, (double T, double Ah, double Rh)> Hourly(IEnumerable<ClimateReading> readings) =>
        readings.GroupBy(r => (int)Math.Floor((r.Ts - start).TotalHours))
            .ToDictionary(g => g.Key, g => (g.Average(r => r.Temperature), g.Average(r => r.AbsoluteHumidity), g.Average(r => (double)r.Humidity)));

    public async Task<JsonObject> Build()
    {
        history = await repository.MetricAverages(day);
        yesterday = await repository.MetricValues(day.AddDays(-1));
        var readings = await repository.Readings(start.AddHours(-3), end.AddHours(4));
        var rooms = await repository.Rooms();

        outsideAll = readings.Where(r => r.Room == settings.OutsideRoom).ToList();
        outsideDay = outsideAll.Where(r => r.Ts >= start && r.Ts < end).ToList();
        outsideNight = outsideAll.Where(r => r.Ts >= start.AddHours(-2) && r.Ts < start.AddHours(6)).ToList();
        outsideHourly = Hourly(outsideDay);
        var outside = BuildOutside();

        var roomArray = new JsonArray();
        foreach (var (room, hasWindow) in rooms.Where(r => r.Name != settings.OutsideRoom))
        {
            roomArray.Add(BuildRoomWithTrend(room, hasWindow, readings));
        }
        Finish(roomArray);

        return Facts = new JsonObject
        {
            ["day"] = day.ToString("yyyy-MM-dd"),
            ["generated_at"] = Local(DateTimeOffset.Now, "yyyy-MM-dd HH:mm:ss"),
            ["status"] = Status,
            ["surface_model"] = $"T_povrch = T_ven + f * (T_uvnitr - T_ven), f = {settings.DefaultF.ToString(CultureInfo.InvariantCulture)}",
            ["highlights"] = new JsonArray(Highlights.Select(h => (JsonNode)new JsonObject { ["level"] = h.Level, ["room"] = h.Room, ["text"] = h.Text }).ToArray()),
            ["outside"] = outside,
            ["rooms"] = roomArray,
        };
    }

    JsonObject? BuildOutside()
    {
        if (outsideDay.Count == 0)
        {
            return null;
        }
        var outside = new JsonObject
        {
            [TAvg] = R(outsideDay.Average(r => r.Temperature)),
            [TMin] = R(outsideDay.Min(r => r.Temperature)),
            ["t_max"] = R(outsideDay.Max(r => r.Temperature)),
            ["rh_avg"] = R(outsideDay.Average(r => r.Humidity), 0),
            [AhAvg] = R(outsideDay.Average(r => r.AbsoluteHumidity)),
        };
        Add(settings.OutsideRoom, TAvg, outsideDay.Average(r => r.Temperature));
        Add(settings.OutsideRoom, TMin, outsideDay.Min(r => r.Temperature));
        Add(settings.OutsideRoom, AhAvg, outsideDay.Average(r => r.AbsoluteHumidity));
        outside["trend"] = Trend(settings.OutsideRoom, [(TAvg, 1), (AhAvg, 1)]);
        return outside;
    }

    JsonObject BuildRoomWithTrend(string room, bool hasWindow, List<ClimateReading> readings)
    {
        var json = BuildRoom(room, readings.Where(r => r.Room == room).ToList());
        json["has_window"] = hasWindow;
        var trend = Trend(room, [(SurfaceHours80, 0), (RhAvg, 0), (AhExcess, 1), (VentEfficiency, 0), (TAvg, 1), (NightRate, 2)]);
        json["trend"] = trend;
        TrendHighlights(room, trend);
        return json;
    }

    void Finish(JsonArray rooms)
    {
        var order = new Dictionary<string, int> { [Fail] = 0, [Warn] = 1, [Info] = 2 };
        var sorted = Highlights.OrderBy(h => order[h.Level]).ToList();
        Highlights.Clear();
        Highlights.AddRange(sorted);
        Status = WorstLevel(Highlights);
        foreach (var room in rooms)
        {
            var name = room!["name"]!.GetValue<string>();
            room["status"] = WorstLevel(Highlights.Where(h => h.Room == name));
        }
    }

    string WorstLevel(IEnumerable<ClimateHighlight> highlights)
    {
        var levels = highlights.Select(h => h.Level).ToHashSet();
        if (levels.Contains(Fail))
        {
            return Fail;
        }
        return levels.Contains(Warn) ? Warn : Ok;
    }

    JsonObject? Trend(string room, (string Name, int Digits)[] metrics)
    {
        var trend = new JsonObject();
        foreach (var (metric, digits) in metrics)
        {
            var today = Metrics.LastOrDefault(m => m.Room == room && m.Name == metric);
            if (today == null)
            {
                continue;
            }
            var item = new JsonObject { ["today"] = R(today.Value, digits) };
            if (yesterday.TryGetValue((room, metric), out var y))
            {
                item["yesterday"] = R(y, digits);
            }
            if (Avg7(room, metric) is double avg)
            {
                item["avg7"] = R(avg, digits);
                item["delta7"] = R(today.Value - avg, digits);
            }
            trend[metric] = item;
        }
        return trend.Count > 0 ? trend : null;
    }

    void TrendHighlights(string room, JsonObject? trend)
    {
        if (trend == null)
        {
            return;
        }
        double? Get(string metric, string key) => trend[metric]?[key]?.GetValue<double>();
        if (Get(SurfaceHours80, "avg7") is double riskAvg && Get(SurfaceHours80, "today") is double risk && riskAvg >= 4 && risk <= riskAvg / 2)
        {
            Highlight(Info, room, $"Riziko plisne klesa: {risk} h (7 dni prumer {riskAvg} h)");
        }
        if (Get(AhExcess, "delta7") is double excessDelta && excessDelta >= 1 && Get(AhExcess, "today") is double excess && Get(AhExcess, "avg7") is double excessAvg)
        {
            Highlight(Info, room, $"Vyssi vlhkostni zatez nez obvykle: {excess} g/m3 nad venkovni (7 dni prumer {excessAvg})");
        }
    }

    JsonObject BuildRoom(string room, List<ClimateReading> all)
    {
        var json = new JsonObject { ["name"] = room };
        var today = all.Where(r => r.Ts >= start && r.Ts < end).ToList();
        if (today.Count == 0)
        {
            json["readings"] = 0;
            Highlight(Fail, room, "Zadna data za cely den");
            return json;
        }
        AddCoverage(room, json, today);
        AddDataQuality(room, json, today);
        AddTemperature(room, json, today);
        var hourly = Hourly(today);
        AddHumidity(room, json, today, hourly);
        AddSurface(room, json, hourly);
        json["ventilation"] = Ventilation(room, all);
        AddNightCooling(room, json, all);
        return json;
    }

    void AddCoverage(string room, JsonObject json, List<ClimateReading> today)
    {
        var gaps = today.Zip(today.Skip(1), (a, b) => (b.Ts - a.Ts).TotalMinutes).Where(m => m > 15).ToList();
        var missingMinutes = gaps.Sum() + Math.Max(0, (today[0].Ts - start).TotalMinutes - 15) + Math.Max(0, (end - today[^1].Ts).TotalMinutes - 15);
        var coverage = R(Math.Max(0, 100 - missingMinutes / (end - start).TotalMinutes * 100));
        json["readings"] = today.Count;
        json["coverage_pct"] = coverage;
        json["last_seen"] = Local(today[^1].Ts, "yyyy-MM-dd HH:mm");
        json["battery_low"] = today.Any(r => r.BatteryLow);
        Add(room, "coverage_pct", coverage);
        if (coverage < 90)
        {
            Highlight(Warn, room, $"Data pokryvaji jen {coverage} % dne (nejdelsi vypadek {R(gaps.DefaultIfEmpty(0).Max(), 0)} min)");
        }
        if (today.Any(r => r.BatteryLow))
        {
            Highlight(Warn, room, "Slaba baterie senzoru");
        }
    }

    void AddDataQuality(string room, JsonObject json, List<ClimateReading> today)
    {
        var jumps = today.Zip(today.Skip(1), (a, b) => (a, b))
            .Count(p => Math.Abs(p.b.Temperature - p.a.Temperature) > 5 && (p.b.Ts - p.a.Ts).TotalMinutes < 10);
        var implausible = today.Count(r => r.Temperature < -40 || r.Temperature > 60 || r.Humidity <= 0 || r.Humidity > 100);
        json["data_quality"] = new JsonObject { ["jumps"] = jumps, ["implausible"] = implausible };
        if (jumps + implausible > 0)
        {
            Highlight(Warn, room, $"Podezrela data: {jumps}x skok teploty o vic nez 5 °C, {implausible}x nesmyslna hodnota");
        }
    }

    void AddTemperature(string room, JsonObject json, List<ClimateReading> today)
    {
        var tAvg = today.Average(r => r.Temperature);
        var night = today.Where(r => LocalHour(r.Ts) < 6).ToList();
        json["temperature"] = new JsonObject
        {
            ["avg"] = R(tAvg),
            ["min"] = R(today.Min(r => r.Temperature)),
            ["max"] = R(today.Max(r => r.Temperature)),
            ["night_avg"] = night.Count > 0 ? R(night.Average(r => r.Temperature)) : null,
        };
        Add(room, TAvg, tAvg);
        Add(room, TMin, today.Min(r => r.Temperature));
        Add(room, "t_max", today.Max(r => r.Temperature));
    }

    void AddHumidity(string room, JsonObject json, List<ClimateReading> today, Dictionary<int, (double T, double Ah, double Rh)> hourly)
    {
        var hours60 = hourly.Count(h => h.Value.Rh >= 60);
        var hours70 = hourly.Count(h => h.Value.Rh >= 70);
        var hours35 = hourly.Count(h => h.Value.Rh < 35);
        var ahAvg = today.Average(r => r.AbsoluteHumidity);
        double? excess = outsideDay.Count > 0 ? ahAvg - outsideDay.Average(r => r.AbsoluteHumidity) : null;
        json["humidity"] = new JsonObject
        {
            ["avg"] = R(today.Average(r => r.Humidity), 0),
            ["min"] = today.Min(r => r.Humidity),
            ["max"] = today.Max(r => r.Humidity),
            ["hours_60"] = hours60,
            ["hours_70"] = hours70,
            ["hours_below_35"] = hours35,
            [AhAvg] = R(ahAvg),
            [AhExcess] = excess is double e ? R(e) : null,
        };
        Add(room, RhAvg, today.Average(r => r.Humidity));
        Add(room, "rh_hours_60", hours60);
        Add(room, "rh_hours_70", hours70);
        Add(room, AhAvg, ahAvg);
        if (excess is double value)
        {
            Add(room, AhExcess, value);
        }
        if (hours70 >= 4)
        {
            Highlight(Warn, room, $"Vlhkost nad 70 % po dobu {hours70} h (max {today.Max(r => r.Humidity)} %)");
        }
        if (hours35 >= 6)
        {
            Highlight(Info, room, $"Sucho: vlhkost pod 35 % po dobu {hours35} h");
        }
    }

    void AddSurface(string room, JsonObject json, Dictionary<int, (double T, double Ah, double Rh)> hourly)
    {
        if (settings.NoSurfaceRisk.Contains(room) || outsideHourly.Count == 0)
        {
            return;
        }
        var f = settings.F(room);
        var surface = hourly.Where(h => outsideHourly.ContainsKey(h.Key)).Select(h =>
        {
            var tOut = outsideHourly[h.Key].T;
            var tSurf = tOut + f * (h.Value.T - tOut);
            return (Hour: h.Key, TSurf: tSurf, RhSurf: 100 * h.Value.Ah / SaturationDensity(tSurf));
        }).ToList();
        if (surface.Count == 0)
        {
            return;
        }
        var risk = surface.Count(s => s.RhSurf >= 80);
        var condensation = surface.Count(s => s.RhSurf >= 100);
        var worst = surface.MaxBy(s => s.RhSurf);
        json["surface"] = new JsonObject
        {
            ["f"] = f,
            [TMin] = R(surface.Min(s => s.TSurf)),
            ["rh_max"] = R(Math.Min(worst.RhSurf, 100), 0),
            ["rh_max_at"] = $"{worst.Hour:00}:00",
            ["hours_80"] = risk,
            ["hours_100"] = condensation,
        };
        Add(room, SurfaceHours80, risk);
        Add(room, "surface_hours_100", condensation);
        Add(room, "surface_rh_max", worst.RhSurf);
        SurfaceHighlight(room, risk, condensation, worst.RhSurf);
    }

    void SurfaceHighlight(string room, int risk, int condensation, double rhMax)
    {
        var trend = Avg7(room, SurfaceHours80) is double avg ? $" (7 dni prumer {R(avg)} h)" : "";
        if (condensation > 0)
        {
            Highlight(Warn, room, $"Odhad kondenzace na studenych mistech {condensation} h, riziko plisne {risk} h{trend}");
        }
        else if (risk >= 6 || rhMax >= 95)
        {
            Highlight(Warn, room, $"Riziko plisne na studenych mistech {risk} h (vlhkost u steny az {R(Math.Min(rhMax, 100), 0)} %){trend}");
        }
    }

    void AddNightCooling(string room, JsonObject json, List<ClimateReading> all)
    {
        var evening = all.Where(r => r.Ts >= start.AddHours(-2) && r.Ts < start).ToList();
        var morning = all.Where(r => r.Ts >= start.AddHours(5) && r.Ts < start.AddHours(6)).ToList();
        if (evening.Count == 0 || morning.Count == 0 || outsideNight.Count == 0)
        {
            return;
        }
        var tEvening = evening.Average(r => r.Temperature);
        var tMorning = morning.Average(r => r.Temperature);
        var tOut = outsideNight.Average(r => r.Temperature);
        var hours = (start.AddHours(5.5) - start.AddHours(-1)).TotalHours;
        var rate = (tMorning - tEvening) / hours;
        var delta = (tEvening + tMorning) / 2 - tOut;
        json["night_cooling"] = new JsonObject
        {
            ["t_evening"] = R(tEvening),
            ["t_morning"] = R(tMorning),
            ["t_outside"] = R(tOut),
            ["rate_c_per_h"] = R(rate, 2),
            ["loss_coefficient"] = delta > 3 ? R(-rate / delta, 4) : null,
        };
        Add(room, NightRate, rate);
        if (delta > 3)
        {
            Add(room, "loss_coefficient", -rate / delta);
        }
    }

    JsonObject Ventilation(string room, List<ClimateReading> all)
    {
        var events = DetectVentilations(all);
        var results = events.Select(e => EvaluateVentilation(room, e.Open, e.Close, all)).OfType<ClimateVentilation>().ToList();
        var drops = results.Select(r => r.Drop).ToList();
        var potentials = results.Where(r => r.Potential.HasValue).Select(r => r.Potential!.Value).ToList();
        var efficiencies = results.Where(r => r.Efficiency.HasValue).Select(r => r.Efficiency!.Value).ToList();
        var recoveries = results.Where(r => r.Recovery.HasValue).Select(r => r.Recovery!.Value).ToList();

        var openMinutes = events.Sum(e => (e.Close - e.Open).TotalMinutes);
        Add(room, "vent_events", events.Count);
        Add(room, "vent_minutes", openMinutes);
        if (drops.Count > 0)
        {
            Add(room, "vent_drop_pct", drops.Average());
        }
        if (efficiencies.Count > 0)
        {
            Add(room, VentEfficiency, efficiencies.Average());
        }
        return new JsonObject
        {
            ["events"] = events.Count,
            ["open_minutes"] = R(openMinutes, 0),
            ["avg_ah_drop_pct"] = drops.Count > 0 ? R(drops.Average(), 0) : null,
            ["avg_ah_drop_possible_pct"] = potentials.Count > 0 ? R(potentials.Average(), 0) : null,
            ["avg_efficiency_pct"] = efficiencies.Count > 0 ? R(efficiencies.Average(), 0) : null,
            ["avg_recovery_min"] = recoveries.Count > 0 ? R(recoveries.Average(), 0) : null,
            ["recovered_within_4h"] = $"{recoveries.Count}/{drops.Count}",
            ["details"] = new JsonArray(results.Select(r => (JsonNode)r.Detail).ToArray()),
        };
    }

    List<(DateTimeOffset Open, DateTimeOffset Close)> DetectVentilations(List<ClimateReading> all)
    {
        var events = new List<(DateTimeOffset Open, DateTimeOffset Close)>();
        DateTimeOffset? openedAt = null;
        ClimateReading? previous = null;
        foreach (var r in all)
        {
            if (r.WindowOpen && previous is { WindowOpen: false })
            {
                openedAt = r.Ts;
            }
            if (!r.WindowOpen && previous is { WindowOpen: true } && openedAt is DateTimeOffset o)
            {
                if (o >= start && o < end)
                {
                    events.Add((o, r.Ts));
                }
                openedAt = null;
            }
            previous = r;
        }
        return events;
    }

    ClimateVentilation? EvaluateVentilation(string room, DateTimeOffset open, DateTimeOffset close, List<ClimateReading> all)
    {
        var before = all.Where(r => r.Ts >= open.AddMinutes(-15) && r.Ts < open).ToList();
        var during = all.Where(r => r.Ts >= open && r.Ts <= close.AddMinutes(15)).ToList();
        if (before.Count == 0 || during.Count == 0)
        {
            return null;
        }
        var pre = before.Average(r => r.AbsoluteHumidity);
        var low = during.Min(r => r.AbsoluteHumidity);
        var drop = 100 * (pre - low) / pre;
        var outsideDuring = outsideAll.Where(r => r.Ts >= open.AddMinutes(-15) && r.Ts <= close).ToList();
        double? potential = outsideDuring.Count > 0
            ? Math.Max(0, 100 * (pre - outsideDuring.Average(r => r.AbsoluteHumidity) - settings.VentilationFloor(room)) / pre)
            : null;
        double? efficiency = potential is double p && p >= 5 ? Math.Clamp(100 * drop / p, 0, 100) : null;
        var back = all.FirstOrDefault(r => r.Ts > close && r.Ts <= close.AddHours(4) && r.AbsoluteHumidity >= pre * 0.95);
        double? recovery = back != null ? (back.Ts - close).TotalMinutes : null;
        var detail = new JsonObject
        {
            ["open"] = Local(open),
            ["minutes"] = R((close - open).TotalMinutes, 0),
            ["ah_drop_pct"] = R(drop, 0),
            ["ah_drop_possible_pct"] = potential is double pp ? R(pp, 0) : null,
            ["efficiency_pct"] = efficiency is double ef ? R(ef, 0) : null,
            ["recovery_min"] = recovery is double v ? R(v, 0) : null,
        };
        return new ClimateVentilation(drop, potential, efficiency, recovery, detail);
    }
}
