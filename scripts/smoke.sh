#!/usr/bin/env bash
# Smoke test the README claims against a real running instance.
# The default store is in-memory, so there is no database file to clean up.
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet run --project src/TrackFlow.Api --no-build -c Debug --urls http://127.0.0.1:5077 >/tmp/tf-smoke.log 2>&1 &
PID=$!
trap 'kill $PID 2>/dev/null || true' EXIT
for i in $(seq 1 30); do curl -sf 127.0.0.1:5077/health >/dev/null 2>&1 && break; sleep 0.5; done
B=127.0.0.1:5077
echo "health: $(curl -s $B/health)"
echo "click:  $(curl -s -o /dev/stderr -w '%{http_code}' -X POST $B/v1/events -H 'content-type: application/json' -d '{"type":"click","campaignId":"spring-sale","clickId":"c-1001","idempotencyKey":"evt-1","occurredAt":"2026-06-01T10:05:00Z"}' 2>&1)"
echo "conv:   $(curl -s -o /dev/stderr -w '%{http_code}' -X POST $B/v1/events -H 'content-type: application/json' -d '{"type":"conversion","campaignId":"spring-sale","clickId":"c-1001","amount":49.99,"idempotencyKey":"evt-2","occurredAt":"2026-06-01T10:06:00Z"}' 2>&1)"
echo "dupe:   $(curl -s -o /dev/stderr -w '%{http_code}' -X POST $B/v1/events -H 'content-type: application/json' -d '{"type":"click","campaignId":"spring-sale","clickId":"c-1001","idempotencyKey":"evt-1"}' 2>&1)"
echo "bad:    $(curl -s -o /dev/stderr -w '%{http_code}' -X POST $B/v1/events -H 'content-type: application/json' -d '{"type":"conversion","campaignId":"x","idempotencyKey":"evt-3"}' 2>&1)"
echo "report: $(curl -s "$B/v1/reports/campaigns?from=2026-01-01T00:00:00Z&to=2026-12-31T00:00:00Z")"
