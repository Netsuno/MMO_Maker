#!/usr/bin/env bash
# P9-5 TCP load harness wrapper.
# Default: in-memory self-hosted Frog.Server (no PostgreSQL, no packaged layout).
# Attach to a packaged/from-source listener with --host/--port.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCENARIO="mixed"
SESSIONS=25
HOLD_MS=3000
CHAT_BURST=12
MOVE_BURST=80
HOST=""
PORT=""
JSON_OUT=""
MAX_PARALLEL_AUTH=8
CONFIGURATION="Release"

USAGE='Usage: run-load-harness.sh [options]

Runs tools/Frog.LoadHarness against a startable TCP server.

Default (in-memory self-host, no PostgreSQL):
  ./scripts/run-load-harness.sh --sessions 25 --scenario mixed

Attach to a packaged or from-source server already listening:
  ./scripts/run-load-harness.sh --host 127.0.0.1 --port 6000 --sessions 10 --scenario connect

Options:
  --scenario connect|chat|move|mixed
  --sessions N
  --hold-ms N
  --chat-burst N
  --move-burst N
  --host ADDR --port N     attach (skips self-host)
  --json-out PATH          also written by the harness; default artifacts/load/load-report.json
  --max-parallel-auth N    cap concurrent register/login (PBKDF2 is CPU-heavy)
  --configuration Debug|Release
  -h|--help
'

while [[ $# -gt 0 ]]; do
  case "$1" in
    --scenario) SCENARIO="${2:-}"; shift 2 ;;
    --sessions) SESSIONS="${2:-}"; shift 2 ;;
    --hold-ms) HOLD_MS="${2:-}"; shift 2 ;;
    --chat-burst) CHAT_BURST="${2:-}"; shift 2 ;;
    --move-burst) MOVE_BURST="${2:-}"; shift 2 ;;
    --host) HOST="${2:-}"; shift 2 ;;
    --port) PORT="${2:-}"; shift 2 ;;
    --json-out) JSON_OUT="${2:-}"; shift 2 ;;
    --max-parallel-auth) MAX_PARALLEL_AUTH="${2:-}"; shift 2 ;;
    --configuration) CONFIGURATION="${2:-}"; shift 2 ;;
    -h|--help) printf '%s' "$USAGE"; exit 0 ;;
    *) echo "error: unknown argument: $1" >&2; printf '%s' "$USAGE" >&2; exit 2 ;;
  esac
done

if [[ -z "$JSON_OUT" ]]; then
  mkdir -p "${ROOT}/artifacts/load"
  JSON_OUT="${ROOT}/artifacts/load/load-report.json"
fi

ARGS=(
  --scenario "$SCENARIO"
  --sessions "$SESSIONS"
  --hold-ms "$HOLD_MS"
  --chat-burst "$CHAT_BURST"
  --move-burst "$MOVE_BURST"
  --json-out "$JSON_OUT"
  --max-parallel-auth "$MAX_PARALLEL_AUTH"
)

if [[ -n "$HOST" ]]; then
  [[ -n "$PORT" ]] || { echo "error: --port is required with --host" >&2; exit 2; }
  ARGS+=(--host "$HOST" --port "$PORT")
else
  ARGS+=(--self-host)
fi

dotnet run --project "${ROOT}/tools/Frog.LoadHarness/Frog.LoadHarness.csproj" \
  -c "$CONFIGURATION" --no-launch-profile -- "${ARGS[@]}"
