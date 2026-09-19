#!/usr/bin/env bash
# P9-5 / P10-5 TCP load harness wrapper.
# Default: in-memory self-hosted Frog.Server (no PostgreSQL, TLS Off).
# P10-8 hosted: --tls-mode Required --tls-target-host --tls-ca-path (never AcceptAll).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SCENARIO="mixed"
SESSIONS=25
HOLD_MS=3000
CHAT_BURST=12
MOVE_BURST=80
SAMPLE_MS=""
ACTION_INTERVAL_MS=""
MANDATE_HOLD_MS=""
HOST=""
PORT=""
JSON_OUT=""
MAX_PARALLEL_AUTH=8
CONFIGURATION="Release"
TLS_MODE=""
TLS_TARGET_HOST=""
TLS_CA_PATH=""
TLS_CERT_PATH=""
TLS_KEY_PATH=""

USAGE='Usage: run-load-harness.sh [options]

Runs tools/Frog.LoadHarness against a startable TCP server.

Default (in-memory self-host, no PostgreSQL, TLS Off):
  ./scripts/run-load-harness.sh --sessions 25 --scenario mixed

P10-8 attach with TLS Required (confined test CA or public CA — never AcceptAll):
  ./scripts/run-load-harness.sh --host HOST --port 6000 --tls-mode Required --tls-target-host HOST --tls-ca-path /tmp/test-ca.pem --sessions 25

Options:
  --scenario connect|chat|move|mixed|campaign
  --sessions N
  --hold-ms N
  --chat-burst N
  --move-burst N
  --sample-ms N
  --action-interval-ms N
  --mandate-hold-ms N
  --host ADDR --port N     attach (skips self-host)
  --json-out PATH          also written by the harness; default artifacts/load/load-report.json
  --max-parallel-auth N    cap concurrent register/login (PBKDF2 is CPU-heavy)
  --tls-mode Off|Required
  --tls-target-host NAME
  --tls-ca-path PEM        confined test CA (CustomRootTrust)
  --tls-cert-path PEM      self-host server cert when Required
  --tls-key-path PEM
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
    --sample-ms) SAMPLE_MS="${2:-}"; shift 2 ;;
    --action-interval-ms) ACTION_INTERVAL_MS="${2:-}"; shift 2 ;;
    --mandate-hold-ms) MANDATE_HOLD_MS="${2:-}"; shift 2 ;;
    --host) HOST="${2:-}"; shift 2 ;;
    --port) PORT="${2:-}"; shift 2 ;;
    --json-out) JSON_OUT="${2:-}"; shift 2 ;;
    --max-parallel-auth) MAX_PARALLEL_AUTH="${2:-}"; shift 2 ;;
    --tls-mode) TLS_MODE="${2:-}"; shift 2 ;;
    --tls-target-host) TLS_TARGET_HOST="${2:-}"; shift 2 ;;
    --tls-ca-path) TLS_CA_PATH="${2:-}"; shift 2 ;;
    --tls-cert-path) TLS_CERT_PATH="${2:-}"; shift 2 ;;
    --tls-key-path) TLS_KEY_PATH="${2:-}"; shift 2 ;;
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

[[ -n "$TLS_MODE" ]] && ARGS+=(--tls-mode "$TLS_MODE")
[[ -n "$TLS_TARGET_HOST" ]] && ARGS+=(--tls-target-host "$TLS_TARGET_HOST")
[[ -n "$TLS_CA_PATH" ]] && ARGS+=(--tls-ca-path "$TLS_CA_PATH")
[[ -n "$TLS_CERT_PATH" ]] && ARGS+=(--tls-cert-path "$TLS_CERT_PATH")
[[ -n "$TLS_KEY_PATH" ]] && ARGS+=(--tls-key-path "$TLS_KEY_PATH")
[[ -n "$SAMPLE_MS" ]] && ARGS+=(--sample-ms "$SAMPLE_MS")
[[ -n "$ACTION_INTERVAL_MS" ]] && ARGS+=(--action-interval-ms "$ACTION_INTERVAL_MS")
[[ -n "$MANDATE_HOLD_MS" ]] && ARGS+=(--mandate-hold-ms "$MANDATE_HOLD_MS")

dotnet run --project "${ROOT}/tools/Frog.LoadHarness/Frog.LoadHarness.csproj" \
  -c "$CONFIGURATION" --no-launch-profile -- "${ARGS[@]}"
