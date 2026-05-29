# Contributing

Thanks for the interest. This document is the working developer guide: where the code lives, where to hook in, and what to do before opening a PR. For the bird's-eye view of what the service does, start with the [README](README.md).

## Table of contents

- [Reporting issues](#reporting-issues)
- [Development setup](#development-setup)
- [Project structure](#project-structure)
- [Runtime architecture](#runtime-architecture)
- [Dependency injection and lifetimes](#dependency-injection-and-lifetimes)
- [Application lifecycle and hooks](#application-lifecycle-and-hooks)
- [Data model and EF Core migrations](#data-model-and-ef-core-migrations)
- [Extension points](#extension-points)
- [Coding conventions](#coding-conventions)
- [Pull request checklist](#pull-request-checklist)
- [Scope boundary with TX07K-TXC](#scope-boundary-with-tx07k-txc)

## Reporting issues

Open issues at <https://github.com/petr-sponky/ArduinoSerialReader/issues>. A useful report contains:

- What you expected vs. what happened.
- Relevant Serilog console output (mask the broker hostname/token if needed).
- The raw 5-byte sensor frame in hex if the bug is in parsing — `Worker.cs` already logs every frame it sees on the COM port at `Information` level.
- A redacted `appsettings.json` shape, especially the broker, COM port and HA WebSocket URL.

For feature requests, describe the use case first (what you're trying to do, why the current behaviour falls short). It's often easier to land a small extension point than a feature.

## Development setup

Requirements:

- .NET SDK 8.x (the project targets `net8.0`)
- A reachable PostgreSQL instance (any 13+ version EF Core supports)
- An MQTT broker reachable from your machine — the project assumes RabbitMQ's MQTT plugin, but any MQTT 3.1.1 broker that lets Home Assistant connect will work
- Optional: a Home Assistant instance for the WebSocket integration (the `HomeAssistantService` background service no-ops cleanly if the WebSocket URL is unreachable, it just logs reconnect attempts)
- Optional: Grafana Loki for log shipping; otherwise the Serilog Loki sink will just fail silently and console logs still work

Local commands:

```powershell
dotnet restore
dotnet build TemperatureSensorArduinoReader.sln -c Release
dotnet run --project TemperatureSensorArduinoReader/TemperatureSensorArduinoReader.csproj
```

For a clean dev loop without Windows Service registration, just `dotnet run` — `UseWindowsService()` is a no-op when not started by the SCM.

## Project structure

Everything lives under [TemperatureSensorArduinoReader/](TemperatureSensorArduinoReader/). One assembly, no test project (yet), one solution file at the repo root.

| Path | Role |
| --- | --- |
| [Program.cs](TemperatureSensorArduinoReader/Program.cs) | Host bootstrap: configuration, Serilog, DI registrations, EF migration on startup. The single composition root. |
| [Worker.cs](TemperatureSensorArduinoReader/Worker.cs) | `BackgroundService` that owns the `SerialPort` for the indoor sensor. Frame buffer is parsed on `DataReceived` and pushed into `SensorPipeline`. |
| [RabbitService.cs](TemperatureSensorArduinoReader/RabbitService.cs) | MQTT client (publish + subscribe) with exponential-backoff reconnect. Inbound messages are dispatched through `TopicDispatcher`. |
| [HomeAssistantService.cs](TemperatureSensorArduinoReader/HomeAssistantService.cs) | `BackgroundService` that holds a WebSocket to Home Assistant and listens for `device_registry_updated` events, then updates the `Room` table. |
| [SensorPipeline.cs](TemperatureSensorArduinoReader/SensorPipeline.cs) | The single ingress orchestrator. Both COM and MQTT call into it. Build/update `Sensor`, persist, publish. |
| [Sensor.cs](TemperatureSensorArduinoReader/Sensor.cs) | Domain object. Frame decoding, CRC, EMA, trend, window-open heuristic. |
| [SensorData.cs](TemperatureSensorArduinoReader/SensorData.cs) | Plain DTO carrying the raw frame (`byte[5]`) or the legacy hex string `Payload` into `Sensor`. |
| [SensorService.cs](TemperatureSensorArduinoReader/SensorService.cs) | Publishes consolidated state JSON to `TX07KTXC/<sensorName>/state` and HA discovery messages. |
| [HomeAssistantSensor.cs](TemperatureSensorArduinoReader/HomeAssistantSensor.cs) | Static factories for HA discovery payloads (one per attribute). The canonical place to add a new HA-visible attribute. |
| [Room.cs](TemperatureSensorArduinoReader/Room.cs), [RoomRepository.cs](TemperatureSensorArduinoReader/RoomRepository.cs), [RoomService.cs](TemperatureSensorArduinoReader/RoomService.cs) | The stable-name layer: `Room` maps a HA area to a (possibly changing) sensor name. |
| [SensorState.cs](TemperatureSensorArduinoReader/SensorState.cs), [SensorReading.cs](TemperatureSensorArduinoReader/SensorReading.cs), [SensorRepository.cs](TemperatureSensorArduinoReader/SensorRepository.cs) | Persistence — `SensorState` is the latest EMA/window state per sensor (1 row per sensor, unique on `(SensorId, Channel)`), `SensorReading` is the time-series append-only log. |
| [AppDbContext.cs](TemperatureSensorArduinoReader/AppDbContext.cs) | EF Core context. `OnModelCreating` is where indexes are declared. |
| [Migrations/](TemperatureSensorArduinoReader/Migrations/) | EF Core migrations. Generated, not hand-written. |
| [TopicStrategies/](TemperatureSensorArduinoReader/TopicStrategies/) | Strategy pattern for MQTT inbound topics — see [Extension points](#extension-points). |
| [TemperatureAppSettings.cs](TemperatureSensorArduinoReader/TemperatureAppSettings.cs) | Strongly-typed configuration POCO bound from `appsettings.json` section `TemperatureAppSettings`. |
| [.github/workflows/](.github/workflows/) | CI: build, publish, deploy. |

## Runtime architecture

```
┌────────────────────────────┐        ┌────────────────────────────────┐
│        IHostedServices     │        │      Singletons / scoped       │
│                            │        │                                │
│  Worker                    │──┐     │  RabbitService (singleton)     │
│   (COM port reader)        │  │     │   ├─ MQTT publish              │
│                            │  │     │   └─ TopicDispatcher (singleton)
│  HomeAssistantService      │  │     │       └─ ITopicStrategy keyed  │
│   (HA WebSocket)           │  │     │                                │
└────────────────────────────┘  │     │  AppDbContext (scoped)         │
                                │     │  SensorRepository (scoped)     │
                                ▼     │  RoomRepository (scoped)       │
                       ┌──────────────────────┐                        │
                       │   SensorPipeline     │                        │
                       │   (scoped, per call) │                        │
                       │                      │                        │
                       │  Sensor.Process()    │                        │
                       │  ├─ Sensor (new/upd) │                        │
                       │  ├─ SensorRepository │                        │
                       │  └─ SensorService ───┼──► RabbitService ──► MQTT
                       └──────────────────────┘                        │
                                                                       │
   inbound MQTT ───► RabbitService.MessageReceived ──► TopicDispatcher ┘
                                                          │
                                                          ▼
                                              keyed ITopicStrategy
                                              (HeaterOutTempStrategy
                                               HomeAssistantOnlineStrategy)
```

Two `BackgroundService` instances (`Worker`, `HomeAssistantService`) plus `RabbitService` as an eagerly-constructed singleton form the runtime backbone. Both ingress paths (COM and MQTT) create a fresh DI scope per message and resolve `SensorPipeline` inside it; that's how the singleton `RabbitService` calls into scoped services without holding a long-lived `AppDbContext`.

## Dependency injection and lifetimes

All registrations live in [Program.cs](TemperatureSensorArduinoReader/Program.cs#L30-L49). The lifetime choice matters — please preserve it when refactoring:

| Service | Lifetime | Why |
| --- | --- | --- |
| `RabbitService` | Singleton | Owns the long-lived MQTT connection + reconnect loop. |
| `TopicDispatcher` | Singleton | Stateless façade over keyed strategies — no reason to recreate. |
| `AppDbContext` | Scoped (via `AddDbContext`) | Default EF Core lifetime; one context per ingress message. |
| `SensorRepository`, `RoomRepository` | Scoped | Wrap the scoped `AppDbContext`. |
| `SensorService`, `RoomService`, `SensorPipeline` | Scoped | Need the scoped repositories. |
| `ITopicStrategy` (`HomeAssistantOnlineStrategy`, `HeaterOutTempStrategy`) | Keyed scoped, keyed by topic string | Resolved per inbound MQTT message. |
| `Worker`, `HomeAssistantService` | Hosted | Long-lived `BackgroundService`. |

**The DI cycle that was avoided:** `SensorService` depends on `RabbitService` (it needs to publish). If `RabbitService` directly injected `SensorPipeline`, the graph would form a cycle. Instead `RabbitService` and `Worker` both inject `IServiceProvider` and create a scope on each inbound message before resolving `SensorPipeline`. Keep this pattern when you add new ingress sources.

## Application lifecycle and hooks

Startup, in order:

1. `Host.CreateDefaultBuilder` loads `appsettings.json`.
2. Serilog is configured (console + Loki sink).
3. DI registrations.
4. `MigrateAndRun` ([Program.cs:59-69](TemperatureSensorArduinoReader/Program.cs#L59-L69)) creates a temporary scope, resolves `AppDbContext`, and runs `Database.Migrate()` before any hosted service starts. **No data flows before migrations succeed.**
5. Host starts. `RabbitService` is constructed eagerly because it's resolved by `HomeAssistantOnlineStrategy` / `HeaterOutTempStrategy` registration and as a dependency of `SensorService` — its constructor blocks on `Connect(...).Wait()`, which means startup waits for the MQTT broker. If you change this, beware: things downstream assume the broker is reachable.
6. `Worker.ExecuteAsync`: resolves `SensorService` once, calls `SendAllSensorsDiscovery` (so HA gets discovery messages on every restart), opens the COM port.
7. `HomeAssistantService.ExecuteAsync`: opens the WebSocket, authenticates with the long-lived token, subscribes to `device_registry_updated`.

Shutdown hooks:

- `IHostApplicationLifetime.ApplicationStopping` is registered in `RabbitService` to cancel its internal `CancellationTokenSource` and trigger an orderly MQTT disconnect.
- `Worker.StopAsync` closes/disposes the `SerialPort`.
- `HomeAssistantService.StopAsync` aborts the WebSocket.

External hooks (not C# hooks, but lifecycle integration points):

- **HA → service:** `homeassistant/status = online` triggers re-publication of all discovery messages ([HomeAssistantOnlineStrategy.cs](TemperatureSensorArduinoReader/TopicStrategies/HomeAssistantOnlineStrategy.cs)).
- **HA → service:** WebSocket `device_registry_updated` events drive `RoomService.AddOrUpdateRoom` ([HomeAssistantService.cs:46-65](TemperatureSensorArduinoReader/HomeAssistantService.cs#L46-L65)).
- **Service → HA:** discovery topics under `homeassistant/<component>/<name>_<kind>/config`, retained by HA.
- **Service → HA:** state topic `TX07KTXC/<sensorName>/state` with `expire_after: 600` so HA marks entities `unavailable` after 10 min of silence.

## Data model and EF Core migrations

Tables (see [AppDbContext.cs](TemperatureSensorArduinoReader/AppDbContext.cs)):

| Table | Key | Notable |
| --- | --- | --- |
| `Rooms` | `Id` PK, `Name` unique | Maps HA area name to a sensor identifier; supports rename via `SensorNewName`. |
| `SensorStates` | `Id` PK, `(SensorId, Channel)` unique | One row per physical sensor — latest EMA, window state, last update timestamp. |
| `SensorReadings` | `Id` PK, indexed on `(SensorName, Timestamp)` | Append-only history of every accepted frame. |

**Migrations workflow:**

```powershell
# create a new migration (run from repo root)
dotnet ef migrations add <DescriptiveName> --project TemperatureSensorArduinoReader

# inspect the SQL it will execute
dotnet ef migrations script --project TemperatureSensorArduinoReader

# apply against your dev DB (not normally needed — MigrateAndRun does it on startup)
dotnet ef database update --project TemperatureSensorArduinoReader
```

Always inspect the generated `Up`/`Down` before committing — EF sometimes drops and recreates columns when a simpler `ALTER` would do.

## Extension points

### Add a new MQTT topic handler

1. Add the constant in [TopicStrategies/MqttTopics.cs](TemperatureSensorArduinoReader/TopicStrategies/MqttTopics.cs) — this is the single source of truth, used in two places.
2. Implement `ITopicStrategy` ([TopicStrategies/ITopicStrategy.cs](TemperatureSensorArduinoReader/TopicStrategies/ITopicStrategy.cs)).
3. Register it in [Program.cs](TemperatureSensorArduinoReader/Program.cs#L44-L45):
   ```csharp
   services.AddKeyedScoped<ITopicStrategy, YourStrategy>(MqttTopics.YourTopic);
   ```
4. Subscribe in [RabbitService.Connected](TemperatureSensorArduinoReader/RabbitService.cs#L90-L91).

`TopicDispatcher` is keyed by exact topic string — wildcards (`+`, `#`) are not currently supported. If you need them, do the matching inside the strategy or extend the dispatcher.

### Add a new ingress source (something other than COM/MQTT)

Route it through `SensorPipeline.Process`, do not reimplement the persistence/publish flow. The shape to follow:

```csharp
using var scope = serviceProvider.CreateScope();
var pipeline = scope.ServiceProvider.GetRequiredService<SensorPipeline>();
await pipeline.Process(new SensorData { Data = fiveBytes }, cancellationToken);
```

If your source delivers something other than 5 raw bytes, decode it before calling `Process` — `SensorData.Payload` (the hex-string branch in `Sensor`) is legacy and slated for removal.

### Add a new HA-visible attribute

1. Compute the value in [Sensor.cs](TemperatureSensorArduinoReader/Sensor.cs) and expose it as a property.
2. Add a factory in [HomeAssistantSensor.cs](TemperatureSensorArduinoReader/HomeAssistantSensor.cs) that returns the discovery config payload.
3. Publish it from [SensorService.SendSensorDiscovery](TemperatureSensorArduinoReader/SensorService.cs#L24-L36).
4. Include the new field in the state JSON in [SensorService.PublishSensorData](TemperatureSensorArduinoReader/SensorService.cs#L60-L71) and reference it from the discovery `value_template`.
5. If it needs to survive restart, add it to `SensorState` and the matching migration.

### Add a derived value that requires history

`SensorReading` is the append-only log; query it through a new repository method rather than scanning `dbContext` from a service. Keep the trend/window-open code in `Sensor.ComputeEma` as the template — small, deterministic, callable from a single place.

## Coding conventions

The codebase is small and intentional about staying small. Match what's there:

- **`internal` by default.** Public surface is only what other assemblies would need (currently nothing besides EF entities and the host bootstrap).
- **Constructor injection only.** No service locator pattern (except inside `BackgroundService.ExecuteAsync` to create a scope, which is the documented `IHostedService` pattern).
- **Don't abstract until there are two callers.** Repositories are concrete classes, not `I*Repository` interfaces, because nothing mocks them.
- **No comments explaining the *what*** — code already does that. Comments are reserved for non-obvious *why* (e.g. the DI-cycle workaround, or why the MQTT payload is hex).
- **Logging:** Serilog with structured properties (`logger.LogInformation("... {sensor}", sensor.Name)`), not string interpolation.
- **Cancellation:** every async call gets a `CancellationToken`. Propagate the one from the caller; don't silently swap for `CancellationToken.None`.
- **Nullable reference types are enabled** (`<Nullable>enable</Nullable>`). Don't suppress with `!` unless the alternative is worse.
- **No `var` in `for`/`foreach` over numeric ranges.** Otherwise `var` is fine.

## Pull request checklist

Before opening:

- [ ] `dotnet build TemperatureSensorArduinoReader.sln -c Release` is clean (no new warnings).
- [ ] Any schema change has an EF Core migration committed in the same PR.
- [ ] If you added an HA-visible attribute, the discovery factory, the state JSON, and the `Sensor` property are all aligned.
- [ ] If you touched DI registrations, the lifetimes still match the table in [Dependency injection and lifetimes](#dependency-injection-and-lifetimes).
- [ ] Secrets (broker password, HA token, connection string) are not in the diff. Check `appsettings.json` and any test files.
- [ ] Commit messages describe the *why*. One commit per logical change is preferred over a single squash.

The PR description should mention:

- What changed and why (link the issue if any).
- How it was tested — at minimum, confirm the service starts against your local broker/DB; ideally include a real sensor frame and the resulting state JSON.
- Anything reviewers should look at first.

## Scope boundary with TX07K-TXC

Arduino-side changes — the RF receiver loop, the AT-command MQTT bridge, the per-attribute topic format on the Uno — belong in the [TX07K-TXC](https://github.com/zefek/TX07K-TXC) repository, not here. PRs that span both sides should be split: land the Arduino side first (it's the producer; this service is the consumer), then update this service to match.

The one place where the two repos meet is the **MQTT payload format on `heater/outTemp`**, currently 10 ASCII hex chars. If you change it on either side, change the other in lockstep and call it out clearly in both PR descriptions.
