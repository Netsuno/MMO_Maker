#!/usr/bin/env bash
# P10-8 hosted: 25 campaign sessions against packaged Frog.Server + PostgreSQL + TLS Required.
# Profiles:
#   ci         — hold 5s (CI budget)
#   cloud      — hold 45s (this agent; not the 60 min mandate)
#   dedicated  — hold 3600s (requires a dedicated host)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROFILE="cloud"
SESSIONS=25
HOLD_MS=""
OUT_DIR=""
DSN="${FROG_POSTGRES_TEST_CONNECTION_STRING:-${FROG_POSTGRES_CONNECTION_STRING:-}}"

USAGE='Usage: phase10-hosted-load-campaign.sh [--profile ci|cloud|dedicated] [--sessions N] [--hold-ms N] [--out DIR]

Starts a packaged server-linux-x64 (copied outside the git tree) with TLS Required
and a disposable PostgreSQL database, then runs Frog.LoadHarness campaign @ 25.

Does NOT claim the 60-minute mandate unless --profile dedicated completes.
'

die() { echo "error: $*" >&2; exit 1; }

while [[ $# -gt 0 ]]; do
  case "$1" in
    --profile) PROFILE="${2:-}"; shift 2 ;;
    --sessions) SESSIONS="${2:-}"; shift 2 ;;
    --hold-ms) HOLD_MS="${2:-}"; shift 2 ;;
    --out) OUT_DIR="${2:-}"; shift 2 ;;
    -h|--help) printf '%s' "$USAGE"; exit 0 ;;
    *) die "unknown argument: $1" ;;
  esac
done

case "$PROFILE" in
  ci) HOLD_MS="${HOLD_MS:-5000}" ;;
  cloud) HOLD_MS="${HOLD_MS:-45000}" ;;
  dedicated) HOLD_MS="${HOLD_MS:-3600000}" ;;
  *) die "profile must be ci|cloud|dedicated" ;;
esac

[[ -n "$DSN" ]] || die "set FROG_POSTGRES_TEST_CONNECTION_STRING"
command -v openssl >/dev/null 2>&1 || die "openssl required"
command -v python3 >/dev/null 2>&1 || die "python3 required"
command -v psql >/dev/null 2>&1 || die "psql required"

STAMP="$(date -u +%Y%m%dT%H%M%SZ)-$$"
OUT_DIR="${OUT_DIR:-/tmp/frog-p10-8-${PROFILE}-${STAMP}}"
case "$OUT_DIR" in
  "$ROOT"|"$ROOT"/*) die "out dir must be outside the git tree: ${OUT_DIR}" ;;
esac
mkdir -p "$OUT_DIR"/{certs,publish,report}

parse_dsn() {
  python3 - "$DSN" <<'PY'
import sys
raw = sys.argv[1]
parts = {}
for item in raw.split(";"):
    if "=" in item:
        k, v = item.split("=", 1)
        parts[k.strip().lower()] = v.strip()
host = parts.get("host", "127.0.0.1")
port = parts.get("port", "5432")
user = parts.get("username") or parts.get("user") or "frog_test"
password = parts.get("password", "")
print(f"{host}\t{port}\t{user}\t{password}")
PY
}

IFS=$'\t' read -r PGHOST PGPORT PGUSER PGPASSWORD < <(parse_dsn)
export PGHOST PGPORT PGUSER PGPASSWORD

DBNAME="frog_p108_$(python3 -c 'import uuid; print(uuid.uuid4().hex[:12])')"
echo "==> create disposable database ${DBNAME}"
psql -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE ${DBNAME};"
cleanup() {
  local code=$?
  if [[ -n "${SERVER_PID:-}" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
    kill -TERM "$SERVER_PID" 2>/dev/null || true
    wait "$SERVER_PID" 2>/dev/null || true
  fi
  PGPASSWORD="$PGPASSWORD" psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d postgres \
    -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='${DBNAME}' AND pid <> pg_backend_pid();" \
    >/dev/null 2>&1 || true
  PGPASSWORD="$PGPASSWORD" psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d postgres \
    -c "DROP DATABASE IF EXISTS ${DBNAME};" >/dev/null 2>&1 || true
  exit "$code"
}
trap cleanup EXIT

LOAD_CS="Host=${PGHOST};Port=${PGPORT};Database=${DBNAME};Username=${PGUSER};Password=${PGPASSWORD}"

echo "==> publish server-linux-x64 → ${OUT_DIR}/publish"
"${ROOT}/scripts/publish-frog.sh" --target server-linux-x64 --output-root "${OUT_DIR}/publish" --force
PUB="${OUT_DIR}/publish/server-linux-x64"
[[ -f "${PUB}/Frog.Server" ]] || die "missing packaged Frog.Server"
chmod +x "${PUB}/Frog.Server"

echo "==> seed Phase 7 world (migrate + Phase7PostgresContentSeed)"
dotnet build "${ROOT}/tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj" -c Release --verbosity quiet
FROG_P108_SEED_CONNECTION_STRING="$LOAD_CS" \
  dotnet test "${ROOT}/tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj" \
  -c Release --no-build --verbosity minimal \
  --filter "FullyQualifiedName~.Phase10LoadCampaignSeedTests" \
  || die "Phase10LoadCampaignSeedTests failed (migrate+seed)"
MAPS="$(PGPASSWORD="$PGPASSWORD" psql -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$DBNAME" -Atc "SELECT count(*) FROM world.maps;")"
[[ "${MAPS:-0}" -ge 1 ]] || die "seed produced no world.maps rows (got ${MAPS:-empty})"
echo "OK seed world.maps=${MAPS}"

echo "==> ephemeral TLS certs (confined, not in git)"
CERTS="${OUT_DIR}/certs"
openssl req -x509 -newkey rsa:2048 -sha256 -days 1 -nodes \
  -keyout "${CERTS}/ca.key.pem" -out "${CERTS}/ca.crt.pem" \
  -subj "/CN=Frog P10-8 Test CA" >/dev/null 2>&1
openssl req -newkey rsa:2048 -sha256 -nodes \
  -keyout "${CERTS}/leaf.key.pem" -out "${CERTS}/leaf.csr" \
  -subj "/CN=localhost" >/dev/null 2>&1
printf 'subjectAltName=DNS:localhost,IP:127.0.0.1\n' > "${CERTS}/san.ext"
openssl x509 -req -in "${CERTS}/leaf.csr" -CA "${CERTS}/ca.crt.pem" -CAkey "${CERTS}/ca.key.pem" \
  -CAcreateserial -out "${CERTS}/leaf.crt.pem" -days 1 -sha256 -extfile "${CERTS}/san.ext" >/dev/null 2>&1

PORT="$(python3 -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1",0)); print(s.getsockname()[1]); s.close()')"
python3 - "$PUB" "$LOAD_CS" "$PORT" "$CERTS" <<'PY'
import json, sys
pub, dsn, port, certs = sys.argv[1:5]
cfg = {
    "Server": {
        "Port": int(port),
        "BindAddress": "127.0.0.1",
        "Tls": {
            "Mode": "Required",
            "CertificatePath": f"{certs}/leaf.crt.pem",
            "PrivateKeyPath": f"{certs}/leaf.key.pem",
        },
    },
    "MariaDb": {"Enabled": False},
    "PostgreSql": {
        "Enabled": True,
        "AllowInMemoryFallback": False,
        "ConnectionString": dsn,
    },
}
open(f"{pub}/appsettings.Local.json", "w").write(json.dumps(cfg, indent=2) + "\n")
print("wrote", f"{pub}/appsettings.Local.json")
PY

SHUTDOWN="${PUB}/.frog-shutdown-request"
rm -f "$SHUTDOWN"
echo "==> start packaged Frog.Server TLS Required :${PORT}"
(
  cd "$PUB"
  FROG_SHUTDOWN_FILE="$SHUTDOWN" ./Frog.Server --contentRoot "$PUB"
) >"${OUT_DIR}/server.log" 2>&1 &
SERVER_PID=$!

python3 - "$PORT" <<'PY'
import socket, sys, time
port = int(sys.argv[1])
deadline = time.time() + 60
last = None
while time.time() < deadline:
    try:
        s = socket.create_connection(("127.0.0.1", port), 0.3)
        s.close()
        sys.exit(0)
    except OSError as e:
        last = e
        time.sleep(0.2)
print("server did not bind:", last, file=sys.stderr)
sys.exit(1)
PY

JSON_OUT="${OUT_DIR}/report/load-report.json"
echo "==> harness 25 campaign TLS hold ${HOLD_MS}ms profile=${PROFILE}"
SAMPLE_LOG="${OUT_DIR}/report/server-rss.tsv"
(
  while kill -0 "$SERVER_PID" 2>/dev/null; do
    if [[ -r /proc/${SERVER_PID}/status ]]; then
      rss="$(awk '/VmRSS/{print $2}' /proc/${SERVER_PID}/status)"
      echo "$(date -u +%s) ${rss}" >> "$SAMPLE_LOG"
    fi
    sleep 2
  done
) &
SAMPLER=$!

"${ROOT}/scripts/run-load-harness.sh" \
  --host 127.0.0.1 --port "$PORT" \
  --scenario campaign --sessions "$SESSIONS" --hold-ms "$HOLD_MS" \
  --tls-mode Required --tls-target-host localhost \
  --tls-ca-path "${CERTS}/ca.crt.pem" \
  --json-out "$JSON_OUT" \
  --max-parallel-auth 8 \
  --mandate-hold-ms 3600000

kill "$SAMPLER" 2>/dev/null || true
kill -TERM "$SERVER_PID" 2>/dev/null || true
echo > "$SHUTDOWN"
wait "$SERVER_PID" 2>/dev/null || true
SERVER_PID=""

python3 - "$JSON_OUT" "$OUT_DIR" "$PROFILE" "$HOLD_MS" "$SESSIONS" "$SAMPLE_LOG" <<'PY'
import json, os, sys, datetime
path, out, profile, hold, sessions, sample = sys.argv[1:7]
rep = json.load(open(path, encoding="utf-8"))
client = rep.get("client", {})
camp = rep.get("campaign") or {}
machine = rep.get("machine", {})
elapsed = int(rep.get("elapsedMs") or 0)
hold_ms = int(hold)
rss_peak = 0
if os.path.isfile(sample) and os.path.getsize(sample) > 0:
    for line in open(sample, encoding="utf-8"):
        parts = line.split()
        if len(parts) == 2:
            rss_peak = max(rss_peak, int(parts[1]))
hb = camp.get("heartbeatRtt") or {}
mv = camp.get("moveRtt") or {}
ix = camp.get("interactRtt") or {}
md = f"""# Phase 10 — LOAD_REPORT hosted (P10-8)

**Pas une gate.** Durée atteinte ≠ mandat 60 min sauf profil `dedicated` terminé.
Profil exécuté : `{profile}` · sessions {sessions} · hold {hold_ms} ms · elapsed {elapsed} ms.

## Host

| Item | Valeur |
| --- | --- |
| OS | {machine.get("os","")} |
| CPU logical | {machine.get("processorCount","")} |
| Harness process CPU est. | {machine.get("processCpuPercentEstimate","")}% |
| Harness working set | {machine.get("processWorkingSetBytes","")} bytes |
| Packaged server VmRSS peak | {rss_peak} kB |
| TLS | Required + confined CA (jamais AcceptAll) |
| Backend | PostgreSQL + `server-linux-x64` publié hors dépôt |

## Client counters

| Signal | N |
| --- | ---: |
| Hello OK | {client.get("helloOk",0)} |
| Login OK | {client.get("loginOk",0)} |
| Character select OK | {client.get("characterSelectOk",0)} |
| Heartbeat sent / ack | {client.get("heartbeatSent",0)} / {client.get("heartbeatAckRecv",0)} |
| Interact sent / result | {client.get("interactSent",0)} / {client.get("interactResultRecv",0)} |
| Melee sent / result | {client.get("meleeSent",0)} / {client.get("meleeResultRecv",0)} |
| Move sent / PositionUpdate | {client.get("moveSent",0)} / {client.get("positionUpdateRecv",0)} |
| Chat sent / rate-limited | {client.get("chatSent",0)} / {client.get("chatRateLimited",0)} |

## Campaign latency (ms)

| Op | n | p50 | p95 | p99 | max |
| --- | ---: | ---: | ---: | ---: | ---: |
| Heartbeat RTT | {hb.get("count",0)} | {hb.get("p50Ms",0)} | {hb.get("p95Ms",0)} | {hb.get("p99Ms",0)} | {hb.get("maxMs",0)} |
| Move RTT | {mv.get("count",0)} | {mv.get("p50Ms",0)} | {mv.get("p95Ms",0)} | {mv.get("p99Ms",0)} | {mv.get("maxMs",0)} |
| Interact RTT | {ix.get("count",0)} | {ix.get("p50Ms",0)} | {ix.get("p95Ms",0)} | {ix.get("p99Ms",0)} | {ix.get("maxMs",0)} |

Actions/s : {camp.get("actionsPerSecond",0)} · mandateDurationMet={camp.get("mandateDurationMet", False)} · gap={camp.get("mandateGap","")}

## vs mandat 25×60

| Critère | Cette run | Reste |
| --- | --- | --- |
| 25 joueurs | demandé {sessions}, Hello {client.get("helloOk",0)}, select {client.get("characterSelectOk",0)} | — |
| 60 minutes | hold {hold_ms/1000:.0f}s + setup ; elapsed {elapsed/1000:.1f}s | **{3600 - hold_ms/1000:.0f}s de hold** : `./scripts/phase10-hosted-load-campaign.sh --profile dedicated` |
| TLS + PG + monde publié | **oui** (packaged + Phase7 seed) | économie 10 mut/s × 5 min, interact palier isolé, restart-reconnect 25 |

JSON brut : `{path}`
Généré UTC {datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ")}
"""
open(os.path.join(out, "report", "LOAD_REPORT.generated.md"), "w", encoding="utf-8").write(md)
print(md)
PY

echo "OK hosted campaign ${PROFILE} report ${OUT_DIR}/report"
