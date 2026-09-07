# TrackFlow

A small, production-shaped event tracking API in ASP.NET Core. It accepts click and conversion
events, deduplicates them, reports per-campaign totals, and rolls raw events up into hourly
aggregates in the background.

This is a sample, not a product. It exists to show how I structure a .NET service: explicit
validation, idempotent writes enforced by the database, a report query that is correct at the
window edges, a background job that is safe to re-run, and integration tests against a real
database provider. About 400 lines of application code and 18 tests.

[![ci](https://github.com/domsupra/trackflow/actions/workflows/ci.yml/badge.svg)](https://github.com/domsupra/trackflow/actions/workflows/ci.yml)

## Quick start

Requires the .NET 7 SDK (also builds on .NET 8).

```bash
dotnet test
dotnet run --project src/TrackFlow.Api
```

Record a click and a conversion:

```bash
curl -s -X POST localhost:5000/v1/events -H 'content-type: application/json' \
  -d '{"type":"click","campaignId":"spring-sale","clickId":"c-1001","idempotencyKey":"evt-1"}'

curl -s -X POST localhost:5000/v1/events -H 'content-type: application/json' \
  -d '{"type":"conversion","campaignId":"spring-sale","clickId":"c-1001","amount":49.99,"idempotencyKey":"evt-2"}'
```

Send `evt-1` again and you get `200` with the original event instead of a second row.

Report on a window:

```bash
curl -s "localhost:5000/v1/reports/campaigns?from=2026-01-01T00:00:00Z&to=2026-12-31T00:00:00Z"
# [{"campaignId":"spring-sale","clicks":1,"conversions":1,"revenue":49.99,"conversionRate":1.0}]
```

Or run it in Docker:

```bash
docker build -t trackflow . && docker run -p 8080:8080 trackflow
```

## Endpoints

| Method | Path | Notes |
|---|---|---|
| `POST` | `/v1/events` | `201` on first insert, `200` with the stored event on a repeated `idempotencyKey`, `400` ProblemDetails on validation failure |
| `GET` | `/v1/reports/campaigns?from&to` | Per-campaign clicks, conversions, revenue, conversion rate for `occurredAt` in `[from, to)` |
| `GET` | `/health` | `{"status":"ok"}` |

Event body: `type` (`click` or `conversion`), `campaignId`, `clickId` (required for conversions),
`amount` (conversions only, non-negative), `occurredAt` (optional, defaults to now), `idempotencyKey`.

## Design notes

**Idempotency is a database constraint, not a code path.** The endpoint checks for an existing
key first, but the unique index on `IdempotencyKey` is what actually guarantees one row per key.
If two requests race, the loser catches the constraint violation and returns the winner's row.
Tracking pixels and postbacks retry aggressively; a tracking API that double counts on retry is
worse than one that drops events.

**Report windows are half-open.** `[from, to)` means an hourly report for 10:00 to 11:00 and one
for 11:00 to 12:00 never share an event. The test suite checks both edges.

**The rollup converges instead of appending.** `RollupService` recomputes every complete hour
that has raw events at or after the last rolled-up hour and upserts the totals. Running it twice
produces the same rows. Late-arriving events for the most recent hour get folded in on the next
run. The `RollupWorker` background service calls it every `Rollup:IntervalMinutes` (default 5)
and logs failures instead of crashing the host.

**Storage is swappable.** The default is SQLite so the repo runs with zero setup. The
`DbContext` is provider-agnostic; point `ConnectionStrings:Tracking` at SQL Server, MySQL, or
Postgres and swap the `UseSqlite` call. Decimal precision is declared on the model so money
columns come out right on every provider.

**Tests hit a real database.** Each test gets its own in-memory SQLite database through
`WebApplicationFactory`, so the tests exercise the actual EF query translation and the actual
unique index rather than a mocked context.

## What a real deployment would add

- EF migrations instead of `EnsureCreated()`
- Authentication on the write endpoint (API key or signed requests per traffic source)
- Rate limiting and request size limits
- Serving reports from `CampaignHourlyStats` for wide windows instead of scanning raw events
- Partitioning or retention on the raw `Events` table
- OpenTelemetry traces and metrics

## Non-goals

No UI, no multi-tenancy, no fraud detection, no attribution modeling. Those are products; this
is a sample of how the foundation under them should look.

## Layout

```
src/TrackFlow.Api/
  Data/      DbContext and entities
  Events/    request contract, validator, POST endpoint
  Reports/   campaign report query and GET endpoint
  Rollup/    RollupService (tested) and RollupWorker (hosted timer)
tests/TrackFlow.Tests/
  ApiFactory.cs   one isolated SQLite database per test
  *Tests.cs       one behavior per test
```

MIT licensed.
