# AIS Live Tracking Service

A .NET 8 service that maintains a live connection to an AIS (Automatic Identification System) feed, validates and normalizes each incoming message, persists clean vessel and position data to SQL Server, and exposes a REST API on top of it.

| Phase | Scope |
|---|---|
| 1 | Ingestion, validation & storage (background worker) |
| 2 | Vessel lookup & latest position by MMSI or IMO |
| 3 | Position history with filters & keyset pagination |
| 4 | Per-vessel voyage analytics (distance, speed, time, idle, gaps, reporting health) |

This project lives in the same solution (`infra.sln`) as the existing Position Tracking / Shipment Route Tracking API (`InfraApi`), but is a separate project (`InfraAis`) with its own database, configuration, and deployment concerns.

---

## Overview

The service does five things, continuously:

1. Maintains a resilient WebSocket connection to a live AIS provider (`aisstream.io`).
2. Parses incoming JSON messages — both position reports and static/voyage data.
3. Validates and normalizes each message (range checks, sentinel handling, IMO checksum, timestamp parsing).
4. Upserts vessel identity/static data and inserts position rows.
5. Logs throughput and rejections, dead-letters invalid messages, and survives disconnects.

---

## Tech stack

- **.NET 8 Web API** (controller-based) hosting a `BackgroundService` worker
- **SQL Server** for storage
- **Raw, parameterized SQL** via `Microsoft.Data.SqlClient` (no Entity Framework)
- **System.Text.Json** for message parsing
- **xUnit** for unit tests
- Structured logging via `ILogger`

---

## Solution structure

```
infra/
├── infra.sln
├── InfraApi/            # existing Shipment Route Tracking API (separate concern)
├── InfraApi.Tests/      # tests for the above
├── InfraAis/            # THIS project — AIS ingestion service
│   ├── Controllers/     # VesselsController, PositionsController (REST API)
│   ├── Options/         # strongly-typed config (AisStream, Ingestion, AisDb, Analytics)
│   ├── Models/          # DTOs (incoming feed shape), records (ready-to-store shape), API responses
│   ├── Validation/      # AisValidator, PositionQueryValidator — pure validation/normalization logic
│   ├── Repositories/    # IAisRepository + AisRepository (raw SQL)
│   ├── Services/        # AisIngestionService (background worker), VesselIdentifierResolver,
│   │                    # VesselAnalyticsService
│   ├── Utils/           # CursorCodec, NavStatusMapper, DistanceCalculator (Haversine)
│   └── Program.cs       # DI wiring
└── InfraAis.Tests/      # xUnit tests (validators, cursor codec, resolver, controllers, analytics)
```

---

## Prerequisites

- .NET 8 SDK
- SQL Server (local `SQLEXPRESS`, or a container)
- A free `aisstream.io` API key (created after signing in with GitHub)

---

## Database setup

The AIS service uses its **own database**, separate from the Shipment system (see Design Decisions). The full script is in [`schema.aistrackingdb.sql`](schema.aistrackingdb.sql):

```sql
CREATE DATABASE AisTrackingDB;
GO
USE AisTrackingDB;
GO

CREATE TABLE vessels (
    mmsi             BIGINT        NOT NULL PRIMARY KEY,
    imo              INT           NULL,
    name             NVARCHAR(128) NULL,
    call_sign        NVARCHAR(16)  NULL,
    ship_type        SMALLINT      NULL,
    dim_to_bow       SMALLINT      NULL,
    dim_to_stern     SMALLINT      NULL,
    dim_to_port      SMALLINT      NULL,
    dim_to_starboard SMALLINT      NULL,
    draught          DECIMAL(4,1)  NULL,
    destination      NVARCHAR(64)  NULL,
    eta              DATETIME2     NULL,
    first_seen_utc   DATETIME2     NOT NULL,
    last_seen_utc    DATETIME2     NOT NULL,
    updated_utc      DATETIME2     NOT NULL
        CONSTRAINT DF_vessels_updated DEFAULT SYSUTCDATETIME()
);

CREATE UNIQUE INDEX UX_vessels_imo ON vessels(imo) WHERE imo IS NOT NULL;

CREATE TABLE positions (
    id                BIGINT       IDENTITY(1,1) PRIMARY KEY,
    mmsi              BIGINT       NOT NULL,
    latitude          FLOAT        NOT NULL,
    longitude         FLOAT        NOT NULL,
    sog               DECIMAL(5,1) NULL,
    cog               DECIMAL(5,1) NULL,
    true_heading      SMALLINT     NULL,
    nav_status        TINYINT      NULL,
    rate_of_turn      SMALLINT     NULL,
    position_accuracy BIT          NULL,
    msg_timestamp_utc DATETIME2    NOT NULL,
    received_utc      DATETIME2    NOT NULL
        CONSTRAINT DF_positions_received DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_positions_vessels FOREIGN KEY (mmsi) REFERENCES vessels(mmsi)
);

CREATE INDEX IX_positions_mmsi_time
    ON positions(mmsi, msg_timestamp_utc DESC)
    INCLUDE (latitude, longitude, sog, cog, nav_status);

CREATE INDEX IX_positions_time
    ON positions(msg_timestamp_utc)
    INCLUDE (mmsi, latitude, longitude, sog, nav_status);

CREATE TABLE ingestion_dead_letter (
    id           BIGINT        IDENTITY(1,1) PRIMARY KEY,
    raw_payload  NVARCHAR(MAX) NOT NULL,
    reason       NVARCHAR(256) NOT NULL,
    received_utc DATETIME2     NOT NULL
        CONSTRAINT DF_dlq_received DEFAULT SYSUTCDATETIME()
);
```

---

## Configuration

Non-secret settings live in `appsettings.json`. Secrets (the API key) are **never committed** — they are supplied at runtime via user-secrets in development, or environment variables in other environments.

`appsettings.json`:

```json
{
  "ConnectionStrings": {
    "AisDb": ""
  },
  "AisStream": {
    "Url": "wss://stream.aisstream.io/v0/stream",
    "ApiKey": "",
    "BoundingBox": { "MinLat": -90, "MinLon": -180, "MaxLat": 90, "MaxLon": 180 },
    "FilterMessageTypes": [ "PositionReport", "ShipStaticData" ]
  },
  "Ingestion": {
    "FutureSkewMinutes": 5,
    "ReconnectBaseDelaySeconds": 1,
    "ReconnectMaxDelaySeconds": 30,
    "SummaryIntervalSeconds": 30
  },
  "Analytics": {
    "GapThresholdHours": 1,
    "IdleThresholdHours": 0.5,
    "StoppedSpeedThresholdKnots": 0.5
  }
}
```

| Section | Setting | Meaning |
|---|---|---|
| `ConnectionStrings` | `AisDb` | Connection string for the AIS database; left empty in source |
| `AisStream` | `Url` | WebSocket endpoint of the feed |
| `AisStream` | `ApiKey` | Left empty in source; provided via user-secrets/env |
| `AisStream` | `BoundingBox` | Geographic area to subscribe to (worldwide by default) |
| `AisStream` | `FilterMessageTypes` | Which AIS message types to receive |
| `Ingestion` | `FutureSkewMinutes` | Clock-skew tolerance for timestamps |
| `Ingestion` | `ReconnectBaseDelaySeconds` | Starting delay for reconnect backoff |
| `Ingestion` | `ReconnectMaxDelaySeconds` | Maximum reconnect delay (cap) |
| `Ingestion` | `SummaryIntervalSeconds` | How often the summary log line is emitted |
| `Analytics` | `GapThresholdHours` | Silence between two messages longer than this is a gap (default 1 h) |
| `Analytics` | `IdleThresholdHours` | A continuous stop longer than this counts as idle (default 0.5 h) |
| `Analytics` | `StoppedSpeedThresholdKnots` | SOG below this counts as stopped (default 0.5 kn) |

Set the API key and connection string (development):

```bash
dotnet user-secrets set "AisStream:ApiKey" "<your-key>" --project InfraAis
```

```bash
dotnet user-secrets set "ConnectionStrings:AisDb" "Server=localhost\SQLEXPRESS;Database=AisTrackingDB;Trusted_Connection=True;TrustServerCertificate=True;" --project InfraAis
```

For a container, supply the key and connection string as environment variables (`AisStream__ApiKey`, `ConnectionStrings__AisDb`), which override `appsettings.json`.

---

## Running

```bash
dotnet run --project InfraAis
```

Within a few seconds you should see the worker connect, send its subscription, and begin logging. Rows will appear in `vessels` and `positions`. A summary line is emitted every 30 seconds. In Development, Swagger UI is available at `/swagger`.

Verify data is landing:

```sql
SELECT COUNT(*) FROM positions;
SELECT COUNT(*) FROM vessels;
SELECT TOP 10 * FROM positions ORDER BY id DESC;
```

---

## Architecture — how it works

The worker is a single long-running `BackgroundService` (`AisIngestionService`) registered in `Program.cs`. Its flow:

```
aisstream.io (wss://)
        │  ClientWebSocket connect
        │  send subscription within 3s
        ▼
   receive loop  ── reassemble frames into whole messages
        ▼
   deserialize envelope → switch on MessageType
        ├─ PositionReport ─┐
        └─ ShipStaticData ─┤
        ▼                  │
   validate + normalize    │
        ├─ valid  ─► upsert vessel, THEN insert position
        └─ invalid ─► ingestion_dead_letter (raw payload + reason)
```

An outer loop wraps the connect-and-listen logic so that any failure triggers a reconnect with exponential backoff rather than crashing the host.

A second, independent loop emits a periodic summary line with running counters.

---

## Design decisions

**Separate database from the Shipment system.**
The AIS tables (`vessels`, `positions`) share a name collision with the existing `positions` table in the Shipment system, and cannot coexist in one schema. More importantly, AIS ingestion is a high-write firehose, whereas the Shipment API is low-volume CRUD — different workloads that do not belong in the same database. The AIS service therefore uses its own `AisTrackingDB` with its own connection string. The two projects share a solution, not a database.

**Foreign key retained; vessel upserted before position.**
`positions` has a foreign key to `vessels`, so a vessel row must exist before any of its positions are inserted. Position reports almost always arrive before a vessel's first static message, so the handler **always upserts the vessel first** (from the position report's MMSI), then inserts the position. This keeps referential integrity intact without dropping the FK.

**Upsert via `MERGE`.**
Every message may be from a new or previously-seen vessel. A single `MERGE` statement handles both cases (insert if new, update `last_seen_utc` and any newer static fields if existing), avoiding a separate existence check. `COALESCE` is used so that a position report — which carries no IMO/name — does not overwrite static fields that a previous static message populated.

**Sentinels normalized to NULL.**
AIS encodes "unknown" as specific magic numbers. These are converted to `NULL` rather than stored literally:

| Field | Sentinel | Handling |
|---|---|---|
| Latitude / Longitude | 91 / 181 | Message rejected (no usable fix) |
| SOG (speed) | 102.3 | Stored as `NULL` |
| COG (course) | 360 | Stored as `NULL` |
| True heading | 511 | Stored as `NULL` |

**IMO checksum: store NULL on failure, never the bad value.**
The IMO number is a permanent hull identifier and appears only in static messages. Its 7th digit is a checksum. If the checksum fails, the IMO is stored as `NULL` rather than persisting a corrupt identifier that could collide with another vessel.

**Go-style timestamp parsing.**
The feed's `time_utc` field arrives in Go's default format, e.g. `2024-05-20 09:21:31.781972101 +0000 UTC`. This breaks .NET's default parser for two reasons: 9 fractional-second digits (.NET supports at most 7) and a literal ` UTC` suffix after the offset. The parser strips the ` UTC` suffix, trims fractional seconds to 7 digits, then uses `DateTime.TryParseExact` with an explicit format string.

**Connection-per-method in the repository.**
Each repository method opens and disposes its own `SqlConnection` (via `await using`), consistent with the existing `InfraApi` repositories. Connection pooling makes this cheap.

**Scoped repository resolved inside a singleton worker.**
A `BackgroundService` is a singleton, but the repository is registered as scoped. The worker therefore injects `IServiceScopeFactory` and creates a short-lived scope per message to resolve a repository, rather than holding one for the lifetime of the app.

**IMO conflicts: first owner keeps it.**
`vessels.imo` has a filtered unique index. Before storing a static message's IMO, the worker checks whether that IMO already belongs to a different MMSI. If it does, the conflict is logged (`IMO CONFLICT`) and the IMO is stored as `NULL` for the new vessel, so the upsert never fails on the unique index.

**Deduplication: not implemented (documented trade-off).**
A unique index on `(mmsi, msg_timestamp_utc)` would prevent exact duplicate positions, but risks dropping legitimate same-second reports from a vessel. It was left out for Phase 1; duplicates (if any) are tolerated in favor of not losing real data. This is a candidate for a future refinement if duplicate volume proves significant.

---

## Validation rules

Each incoming message passes through a chain of checks (`AisValidator`). Any failure is logged with a reason and the raw payload is written to `ingestion_dead_letter`.

| Rule | Behavior |
|---|---|
| Provider `Valid` flag | If the feed marks a message invalid, it is dropped |
| MMSI | Must be exactly 9 digits (`100000000`–`999999999`); rejected otherwise. Checked for both position and static messages **before** any vessel upsert |
| Latitude / Longitude | Must be within `[-90,90]` / `[-180,180]`; rejects the `91`/`181` sentinel |
| SOG / COG / heading | Sentinel values normalized to `NULL` |
| IMO (static only) | 7-digit checksum validated; stored `NULL` if it fails |
| Timestamp | Parsed from Go-style format; rejected if missing or unparseable |
| Future-skew | Timestamps more than `FutureSkewMinutes` ahead of now are rejected |

---

## Resilience

- The connect-and-listen logic is wrapped in an outer loop that reconnects on any failure.
- Reconnect delay uses **exponential backoff**: `1s → 2s → 4s …` capped at `ReconnectMaxDelaySeconds` (30s), and resets to the base delay after a successful session.
- Normal shutdown (`CancellationToken` / Ctrl+C) is caught specifically as `OperationCanceledException` and treated as a clean stop, not an error.
- A single bad message is caught per-iteration and skipped; it never breaks the receive loop. Unparseable JSON is logged and skipped. Any other handler failure (e.g. a database error) is counted as rejected and the raw payload is written to `ingestion_dead_letter` with the exception type and message.

---

## Observability

- Running counters track messages received, positions stored, static messages stored, messages rejected, and reconnects. Every increment uses `Interlocked.Increment`, because the summary loop reads the counters concurrently.
- A summary line is emitted every `SummaryIntervalSeconds` (default 30s), then the counters reset. Counter reset uses `Interlocked.Exchange` so the read-and-reset is safe against the concurrently-running ingestion loop.
- Per-message logging is at `Debug` level so that, at `Information` level, the console shows the periodic summary rather than a flood of per-message lines.

Example summary line:

```
SUMMARY (last 30s): received=1240 stored=1198 rejected=15 staticStored=27 reconnects=0
```

---

## Dead-letter trail

Invalid messages are not silently discarded. Each is written to `ingestion_dead_letter` with the raw JSON payload and a human-readable rejection reason, which is useful for diagnosing feed-quality issues.

---

## Representative SQL (vessel upsert)

```sql
MERGE vessels AS target
USING (SELECT @Mmsi AS mmsi) AS source
ON (target.mmsi = source.mmsi)
WHEN MATCHED THEN
    UPDATE SET
        imo           = COALESCE(@Imo, target.imo),
        name          = COALESCE(@Name, target.name),
        call_sign     = COALESCE(@CallSign, target.call_sign),
        ship_type     = COALESCE(@ShipType, target.ship_type),
        last_seen_utc = @TimestampUtc,
        updated_utc   = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (mmsi, imo, name, call_sign, ship_type, first_seen_utc, last_seen_utc, updated_utc)
    VALUES (@Mmsi, @Imo, @Name, @CallSign, @ShipType, @TimestampUtc, @TimestampUtc, SYSUTCDATETIME());
```

All SQL is parameterized with explicit `SqlDbType`; no values are concatenated into SQL strings.

---

## Phase 2 — Vessel lookup & latest position

```
GET /api/vessels/{identifier}                    # identity and static data
GET /api/vessels/{identifier}/positions/latest   # most recent stored position
```

`identifier` is a 9-digit MMSI or a 7-digit IMO. The type is inferred from the length, or forced with `?idType=mmsi|imo`. An IMO is resolved to its MMSI before the lookup.

| Case | Response |
|---|---|
| Malformed identifier, or `idType` does not match | `400` |
| Unknown IMO or MMSI | `404` |
| Vessel known but no positions stored yet | `404` with a distinct message |
| Found | `200` |

The latest-position response also includes `navStatusText` (human-readable nav status) and `ageSeconds` (how old the fix is).

---

## Phase 3 — Position history (filters & pagination)

### Endpoints

```
GET /api/vessels/{identifier}/positions   # history for one vessel (MMSI or IMO, optional ?idType=)
GET /api/positions                        # fleet-wide query
```

### Query parameters

| Param | Meaning | Notes |
|-------|---------|-------|
| `from` / `to` | UTC window on `msg_timestamp_utc` | ISO-8601; `from` inclusive, `to` **exclusive** |
| `minLat` `maxLat` `minLon` `maxLon` | bounding box | all four together or none |
| `navStatus` | nav-status codes | comma list, e.g. `0,1,5` (0–15) |
| `minSog` / `maxSog` | speed band in knots | |
| `mmsi` | restrict to vessels | **fleet endpoint only**, comma list |
| `sort` | `asc` \| `desc` on time | default `desc`, case-insensitive |
| `limit` | page size | default 100; values above **1000 are clamped to 1000** (not an error); `0` or negative → `400` |
| `cursor` | keyset pagination token | the `nextCursor` from the previous response |

### Validation rules

- `from` must be ≤ `to`, otherwise `400`.
- Bounding box: all four values or none; latitude in [-90, 90], longitude in [-180, 180]; `min` ≤ `max`.
- **Fleet endpoint** must have a full time window (`from` **and** `to`) or a bounding box, so nobody can request every position ever.
- **Single-vessel endpoint** needs no window (returns the latest positions first); `from` or `to` may be used alone; `mmsi` is not allowed.
- Invalid `navStatus`, `sort`, `mmsi` or `cursor` values return `400` with a message naming the parameter.
- Unknown IMO/MMSI on the single-vessel endpoint returns `404`.

### Pagination (keyset)

Rows are ordered by `(msg_timestamp_utc, id)` in the requested direction. The repository fetches `limit + 1`
rows; if the extra row exists, the last returned row's `(timestamp, id)` is encoded into `nextCursor`
(base64). The next request adds `WHERE (msg_timestamp_utc, id) < (@cursorTs, @cursorId)` (or `>` for `asc`),
so deep pages cost the same as the first page — no `OFFSET`. `nextCursor` is `null` on the last page.

```json
{ "items": [ ... ], "nextCursor": "MjAyNi0w...|MTIz", "count": 100 }
```

### Indexes

- `IX_positions_mmsi_time (mmsi, msg_timestamp_utc DESC)` — single-vessel history (Phase 1).
- `IX_positions_time (msg_timestamp_utc) INCLUDE (mmsi, latitude, longitude, sog, nav_status)` — fleet windowed scans.

---

## Phase 4 — Vessel analytics

### Endpoint

```
GET /api/vessels/{identifier}/analytics?from=...&to=...
```

- `identifier`: MMSI or IMO (type inferred from length).
- `from` / `to`: optional. `to` defaults to now, `from` to one month before `to`. `from` is inclusive, `to` exclusive.
- `400` for a malformed identifier or `from >= to`; `404` for an unknown vessel.
- A known vessel with no messages in the window gets `200` with a zeroed response, not `404` or `null`.

### Response

```json
{
  "mmsi": 239123456,
  "window": { "from": "2026-01-01T10:00:00+00:00", "to": "2026-01-01T14:00:00+00:00", "coveragePercent": 50 },
  "distanceKm": 22.24,
  "distanceExcludedKm": 53.37,
  "speed": { "avgSogKnots": 12, "maxSogKnots": 12.4, "maxImpliedKnots": 12.1 },
  "time": {
    "underwayHours": 1.5,
    "stoppedHours": 0.5,
    "byNavStatus": [ { "navStatus": 0, "hours": 1.5 }, { "navStatus": 1, "hours": 0.5 } ]
  },
  "idleHours": 0,
  "gaps": { "count": 1, "totalDarkHours": 3, "longest": { "hours": 3, "from": "...", "to": "..." } },
  "reporting": { "messages": 14, "medianIntervalSeconds": 600, "first": "...", "last": "..." }
}
```

### How it is computed

Positions in the window are sorted by time and walked as consecutive **segments** (pairs of messages). A segment takes the SOG and nav status of its **next** point, i.e. the state reported at the end of the segment.

| Metric | Rule |
|---|---|
| Duplicate timestamps | Segments with `dt <= 0` are skipped, so implied speed never divides by zero (no `Infinity`/`NaN` in the JSON) |
| Gap | A segment longer than `GapThresholdHours`. Counted, added to `totalDarkHours`, its distance goes to `distanceExcludedKm`, and the longest is reported. Gap time is not classified as underway, stopped or any nav status |
| `distanceKm` | Haversine distance (R = 6371 km) summed over non-gap segments |
| `maxImpliedKnots` | Highest segment distance / segment time over non-gap segments |
| Underway / stopped | Next point's SOG `>=` / `<` `StoppedSpeedThresholdKnots`. Unknown SOG is left unclassified, so underway + stopped + dark can be less than the window |
| `idleHours` | Sum of continuous stopped runs longer than `IdleThresholdHours`. A run ends when the vessel moves, at a gap, or at the end of the data. Unknown SOG does not break a run |
| `byNavStatus` | Non-gap segment hours grouped by the next point's nav status |
| `avgSogKnots` / `maxSogKnots` | Over all messages with a reported SOG (per message, not time-weighted) |
| `medianIntervalSeconds` | Median of all positive intervals between consecutive messages, gaps included |
| `coveragePercent` | `100 - (silence before first + silence after last + dark hours) / window × 100`; `0` with fewer than 2 messages |

All thresholds come from the `Analytics` configuration section; none are hard-coded.

---

## Testing

Unit tests (`InfraAis.Tests`, xUnit) need no database or network; repositories are faked. They cover:

- **IMO checksum** — known-valid numbers, wrong checksums, and wrong-length inputs.
- **MMSI** — valid 9-digit values and out-of-range/wrong-length values.
- **Position** — valid coordinates, boundary corners, and the `91`/`181` sentinel.
- **Sentinel normalization** — SOG/COG/heading sentinels map to `NULL`; real values pass through.
- **Timestamp parsing** — the Go-style format parses correctly; malformed input is rejected.
- **Future-skew guard** — past and within-tolerance timestamps pass; far-future timestamps fail.
- **Cursor codec** (Phase 3) — encode/decode round-trips, sub-second precision, UTC handling, garbage input rejected.
- **Position query validation** (Phase 3) — inverted ranges, partial bounding box, limit clamp, nav-status/sort/mmsi/cursor parsing, fleet vs single-vessel rules, all filters composed.
- **Positions controller** (Phase 3) — 400/404/200 paths for both endpoints; IMO is resolved to MMSI before querying.
- **Identifier resolver / vessels controller** (Phase 2) — MMSI vs IMO parsing, `idType` handling, IMO→MMSI resolution, 400/404/200 paths.
- **Vessel analytics** (Phase 4) — hand-checkable fixtures on a single longitude: empty window, single point, gaps and the longest gap, stopped/underway/idle runs, unknown SOG, duplicate timestamps, nav-status breakdown, coverage, median interval, and thresholds read from options.

Run:

```bash
dotnet test
```

---

## Not yet implemented / next steps

The following are known remaining items (bonus scope or hardening):

- **Docker**: a `Dockerfile` and `docker-compose.yml` (API + SQL Server) to run the whole stack. The container connection string will require SQL authentication rather than Windows auth.
- **Startup validation**: fail fast at startup if the connection string is missing (e.g. `ValidateOnStart`), rather than surfacing the error on the first database write.
- **`/health` endpoint**: report feed connection state and last-message age.
- **Deduplication**: optional unique index on `(mmsi, msg_timestamp_utc)`.
- **Batched inserts / backpressure**: `SqlBulkCopy` with a flush interval, and a bounded `Channel` between the reader and the writer if inserts fall behind the feed.
