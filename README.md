# ArduinoSerialReader (TemperatureSensorArduinoReader)

.NET 8 background service (runs as a Windows Service) that collects readings from wireless **TX07K-TXC** temperature & humidity sensors, derives additional values, and republishes them to MQTT in a shape Home Assistant can consume directly.

This project is the server-side counterpart to the Arduino sketch [TX07K-TXC → MQTT bridge](https://github.com/zefek/TX07K-TXC/tree/main/usage/TX07K_MQTT). Its job is to shield Home Assistant from two awkward properties of TX07K-TXC: the **unstable `sensorId`** (regenerated on every battery change) and the **limited payload shapes** practical on an ATmega328P.

## What the service does

- Ingests 5-byte sensor frames over two channels:
  - **COM port** ([Worker.cs](TemperatureSensorArduinoReader/Worker.cs)) — an Arduino on USB serial pushes CR/LF-terminated frames. Typically an indoor sensor.
  - **MQTT** ([RabbitService.cs](TemperatureSensorArduinoReader/RabbitService.cs) + [TopicStrategies/HeaterOutTempStrategy.cs](TemperatureSensorArduinoReader/TopicStrategies/HeaterOutTempStrategy.cs)) — an outdoor sensor reached through a 433 MHz receiver + ESP/MQTT bridge (see [Relationship to TX07K_MQTT](#relationship-to-tx07k_mqtt) below). Payload is **5 raw bytes** (same frame layout as the COM ingress).
- Decodes the frame ([Sensor.cs](TemperatureSensorArduinoReader/Sensor.cs)): temperature, humidity, channel, sensor ID, flags (battery low, trend up/down, forced transmission), CRC check.
- Computes derived values: **dew point**, **absolute humidity**, **EMA**, **temperature/humidity trend (°C/h, %/h)** and a heuristic **window-open** detector (simultaneous sharp drop in temperature and humidity).
- Persists state and reading history in PostgreSQL via EF Core ([AppDbContext.cs](TemperatureSensorArduinoReader/AppDbContext.cs), [Migrations/](TemperatureSensorArduinoReader/Migrations/)).
- Publishes one consolidated JSON state to `TX07KTXC/<sensorName>/state` ([SensorService.cs](TemperatureSensorArduinoReader/SensorService.cs)).
- Publishes **Home Assistant MQTT discovery** messages for every attribute ([HomeAssistantSensor.cs](TemperatureSensorArduinoReader/HomeAssistantSensor.cs)) — temperature, humidity, dew point, absolute humidity, battery, trend, temperature/humidity trend, window open.
- Keeps a `Room ↔ Sensor` mapping in the DB ([Room.cs](TemperatureSensorArduinoReader/Room.cs)). HA only ever sees the stable room name; the volatile physical sensor ID is hidden behind it.
- Subscribes to the Home Assistant WebSocket API ([HomeAssistantService.cs](TemperatureSensorArduinoReader/HomeAssistantService.cs)) for `device_registry_updated` events. Renaming a device or assigning an area in the HA UI is reflected back into the service database.
- Reacts to `homeassistant/status = online` ([TopicStrategies/HomeAssistantOnlineStrategy.cs](TemperatureSensorArduinoReader/TopicStrategies/HomeAssistantOnlineStrategy.cs)) by re-emitting discovery messages (handles HA restarts).
- Logs via Serilog to console and to Grafana Loki ([Program.cs](TemperatureSensorArduinoReader/Program.cs)).

## Architecture

graph TD
    IS[Indoor TX07K-TXC]
    OS[Outdoor TX07K-TXC]

    subgraph Bridges["Arduino bridges"]
        IB[TX07K-TXC sketch<br/>USB serial]
        OB[TX07K_MQTT sketch<br/>ESP-01]
    end

    IS -.->|RF 433 MHz| IB
    OS -.->|RF 433 MHz| OB

    subgraph Service["TemperatureSensorArduinoReader (.NET)"]
        W[Worker.cs]
        R[RabbitService]
        P[SensorPipeline]
        SR[SensorRepository<br/>EF Core + PG]
        SS[SensorService<br/>publish + HA discovery]
        HS[HomeAssistantService<br/>WebSocket]
        W --> P
        R --> P
        P --> SR
        P --> SS
        HS -.->|area / name updates| SR
    end

    IB -->|5-byte frames CR/LF| W
    OB -->|heater/outTemp 5B raw| MB[(MQTT broker<br/>RabbitMQ)]
    MB -->|heater/outTemp| R
    SS -->|TX07KTXC/.../state<br/>+ discovery| MB
    MB -->|state + discovery| HA[Home Assistant]
    HA -.->|device_registry_updated| HS

Both ingress paths (COM and MQTT) funnel into the same [SensorPipeline.cs](TemperatureSensorArduinoReader/SensorPipeline.cs), so parsing, persistence and publishing live in one place. `RabbitService` is registered as a singleton and resolves `SensorPipeline` per message through `IServiceScopeFactory` to break the DI cycle that would otherwise form between `RabbitService` and `SensorService`.

> Note: the MQTT broker is **RabbitMQ** (via its MQTT plugin). RabbitMQ internally translates topic separators `/` ↔ `.` between the MQTT layer and AMQP routing keys. The wire format and the code stay consistent with `/` (e.g. `heater/outTemp`, `homeassistant/status`).

## Relationship to TX07K_MQTT

[TX07K_MQTT.ino](https://github.com/zefek/TX07K-TXC/blob/main/usage/TX07K_MQTT/TX07K_MQTT.ino) is the Arduino sketch (Uno + 433 MHz receiver + ESP-01) that:

- receives RF frames from TX07K/TXC sensors,
- decodes temperature/humidity and computes dew point + absolute humidity on the MCU,
- publishes **one MQTT topic per attribute** under `TX07K_TXC/<sensorId>/<channel>/<attribute>` (the sketch deliberately avoids JSON because of the 2 KB SRAM ceiling on Uno).

This service (`ArduinoSerialReader`) picks up where the sketch stops — the parts that don't fit on a Uno:

| Concern | TX07K_MQTT (Arduino) | ArduinoSerialReader (.NET) |
| --- | --- | --- |
| Frame decoding | yes | yes |
| Dew point, absolute humidity | yes | yes |
| EMA, °C/h and %/h trend | — | yes |
| Window-open detection | — | yes |
| Stable naming across battery swap | — | yes (via `Room.SensorName` / `SensorNewName`) |
| HA MQTT discovery | — (someone external must publish it) | yes |
| Consolidated JSON state topic | — (per-attribute) | yes (`TX07KTXC/<sensorName>/state`) |
| Two-way binding with HA device registry | — | yes (WebSocket) |
| `expire_after` on HA entities | — | yes (600 s — entities go `unavailable` when the sensor stops talking) |

### Payload format

`HeaterOutTempStrategy` expects the payload as **5 raw bytes** — the same TX07K-TXC frame layout the COM ingress receives. The Arduino sketch publishes it directly via `client.Publish(TOPIC_OUTSIDETEMPERATURE, rawData, 5)` (no retain, no encoding).

Historically the sketch hex-encoded the frame to 10 ASCII chars before publishing because Home Assistant treats MQTT payloads as UTF-8 text and bytes ≥ 0x80 broke its parser. That encoding step was removed once HA no longer needed to read this topic directly — the .NET service decodes the frame and republishes a clean JSON state for HA on `TX07KTXC/<sensorName>/state`.

### Stable sensor name (`Room`)

TX07K-TXC regenerates the 8-bit `sensorId` on every battery change; the channel (1–3) is set by a physical switch and stays. The service handles it like this:

- `Room.SensorName` in the form `"<sensorId>_<channel>"` (e.g. `"123_1"`) is the current sensor identifier.
- `Room.Name` is the user-facing room name.
- After a battery swap the new `<id>_<channel>` is written into `Room.SensorNewName` and the service eventually switches over.
- Home Assistant only sees `Room.SensorName` — discovery and the state topic use it, so HA entities survive battery swaps without manual edits.

## Configuration

`appsettings.json` in the application directory, section `TemperatureAppSettings` (see [TemperatureAppSettings.cs](TemperatureSensorArduinoReader/TemperatureAppSettings.cs)):

```json
{
  "TemperatureAppSettings": {
    "MqttBroker": "rabbitmq.example.com",
    "MqttPort": 8883,
    "MQTTUsername": "...",
    "MQTTPassword": "...",
    "COMPort": "COM3",
    "HomeAssistantWebSocket": "wss://homeassistant.local/api/websocket",
    "HomeAssistantToken": "<long-lived access token>",
    "ConnectionString": "Host=...;Database=...;Username=...;Password=...",
    "LokiUrl": "http://loki.example.com:3100"
  }
}
```

The MQTT client connects over TLS (typically port 8883). Certificate validation in [RabbitService.cs](TemperatureSensorArduinoReader/RabbitService.cs) is intentionally permissive (self-signed brokers) — tighten it for production.

## Build and run

```powershell
dotnet restore
dotnet build TemperatureSensorArduinoReader.sln -c Release
dotnet run --project TemperatureSensorArduinoReader/TemperatureSensorArduinoReader.csproj
```

EF Core migrations run automatically on startup (`MigrateAndRun` in [Program.cs](TemperatureSensorArduinoReader/Program.cs)). To create a new migration:

```powershell
dotnet ef migrations add <Name> --project TemperatureSensorArduinoReader
```

`Microsoft.Extensions.Hosting.WindowsServices` lets you install the binary as a Windows Service:

```powershell
sc.exe create TemperatureSensorArduinoReader binPath= "C:\path\to\TemperatureSensorArduinoReader.exe"
sc.exe start TemperatureSensorArduinoReader
```

CI and deployment workflows live under [.github/workflows/](.github/workflows/).

## MQTT topics

| Topic | Direction | Purpose |
| --- | --- | --- |
| `heater/outTemp` | subscribe | 5-byte raw TX07K-TXC frame from the outdoor sensor (see [HeaterOutTempStrategy](TemperatureSensorArduinoReader/TopicStrategies/HeaterOutTempStrategy.cs)) |
| `homeassistant/status` | subscribe | reacts to HA restart by republishing discovery |
| `homeassistant/sensor/<name>_<kind>/config` | publish (retained by HA) | discovery for each scalar attribute |
| `homeassistant/binary_sensor/<name>_<kind>/config` | publish | discovery for battery / window_open |
| `TX07KTXC/<sensorName>/state` | publish | consolidated JSON state (`temperature`, `humidity`, `battery`, `trend`, `dewPoint`, `absoluteHumidity`, `temperatureTrend`, `humidityTrend`, `windowOpen`) |

## Dependencies

- .NET 8, Microsoft.Extensions.Hosting (+ WindowsServices)
- MQTTnet 4
- Npgsql + EF Core 8 (PostgreSQL)
- Newtonsoft.Json
- System.IO.Ports (COM port for the indoor sensor)
- Serilog + Grafana Loki sink

## Contributing

Issues and pull requests welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for project structure, extension points, the DI / lifecycle wiring, and the PR checklist.

## License

[MIT](LICENSE) © 2026 Zefek.
