#!/usr/bin/env bash
# P10-8: 25 authed sessions under TLS Required, campaign actions + latency/TPS/CPU/RAM.
# Default hold is a cloud-sized proof (90 s). Mandate 60 min = --hold-ms 3600000 on a
# dedicated host. Never AcceptAll.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SESSIONS=25
HOLD_MS=90000
SAMPLE_MS=1000
ACTION_INTERVAL_MS=250
MANDATE_HOLD_MS=3600000
JSON_OUT=""
CONFIGURATION="Release"
CERT_DIR=""

USAGE='Usage: run-p10-8-load-campaign.sh [options]

Self-host in-memory Frog.Server + TLS Required + 25 campaign sessions.

  ./scripts/run-p10-8-load-campaign.sh                  # 90 s cloud proof
  ./scripts/run-p10-8-load-campaign.sh --hold-ms 3600000  # mandat 60 min (machine dédiée)

Options:
  --sessions N
  --hold-ms N              campaign duration (default 90000)
  --sample-ms N
  --action-interval-ms N
  --mandate-hold-ms N      report comparison only (default 3600000)
  --json-out PATH
  --cert-dir DIR           reuse PEMs (ca.pem / leaf.pem / leaf.key)
  --configuration Debug|Release
'

while [[ $# -gt 0 ]]; do
  case "$1" in
    --sessions) SESSIONS="${2:-}"; shift 2 ;;
    --hold-ms) HOLD_MS="${2:-}"; shift 2 ;;
    --sample-ms) SAMPLE_MS="${2:-}"; shift 2 ;;
    --action-interval-ms) ACTION_INTERVAL_MS="${2:-}"; shift 2 ;;
    --mandate-hold-ms) MANDATE_HOLD_MS="${2:-}"; shift 2 ;;
    --json-out) JSON_OUT="${2:-}"; shift 2 ;;
    --cert-dir) CERT_DIR="${2:-}"; shift 2 ;;
    --configuration) CONFIGURATION="${2:-}"; shift 2 ;;
    -h|--help) printf '%s' "$USAGE"; exit 0 ;;
    *) echo "error: unknown argument: $1" >&2; printf '%s' "$USAGE" >&2; exit 2 ;;
  esac
done

if [[ -z "$JSON_OUT" ]]; then
  mkdir -p "${ROOT}/artifacts/load"
  JSON_OUT="${ROOT}/artifacts/load/p10-8-campaign.json"
fi

if [[ -z "$CERT_DIR" ]]; then
  CERT_DIR="$(mktemp -d /tmp/frog-p10-8-certs-XXXXXX)"
fi
mkdir -p "$CERT_DIR"

dotnet run --project "${ROOT}/tools/Frog.LoadHarness/Frog.LoadHarness.csproj" \
  -c "$CONFIGURATION" --no-launch-profile -- --emit-test-certs "$CERT_DIR"

"${ROOT}/scripts/run-load-harness.sh" \
  --scenario campaign \
  --sessions "$SESSIONS" \
  --hold-ms "$HOLD_MS" \
  --sample-ms "$SAMPLE_MS" \
  --action-interval-ms "$ACTION_INTERVAL_MS" \
  --mandate-hold-ms "$MANDATE_HOLD_MS" \
  --json-out "$JSON_OUT" \
  --tls-mode Required \
  --tls-target-host localhost \
  --tls-ca-path "${CERT_DIR}/ca.pem" \
  --tls-cert-path "${CERT_DIR}/leaf.pem" \
  --tls-key-path "${CERT_DIR}/leaf.key" \
  --configuration "$CONFIGURATION"

echo "P10-8 campaign JSON: $JSON_OUT"
echo "TLS certs (confined, not git): $CERT_DIR"
