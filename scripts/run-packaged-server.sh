#!/usr/bin/env bash
# Start or stop a published Frog.Server layout (see PACKAGING_GUIDE.md).
# Working directory and --contentRoot are the publish folder so appsettings*.json resolve.
set -euo pipefail

USAGE='Usage: run-packaged-server.sh start|stop --dir DIR [options]

Start / stop a directory produced by publish-frog.sh (server-linux-x64 or portable).

start options:
  --dir DIR           Publish directory (required)
  --foreground        Stay attached (default: background, logs to DIR/frog-server.log)
  --port N            Override Server:Port (generic-host switch)
  --bind ADDRESS      Override Server:BindAddress

stop options:
  --dir DIR           Same directory used for start (required)
  --timeout SECONDS   Wait for graceful exit (default: 20)

Environment:
  FROG_POSTGRES_CONNECTION_STRING   used if PostgreSql:ConnectionString is empty
  FROG_SHUTDOWN_FILE                default DIR/.frog-shutdown-request

Config overlay: copy appsettings.Local.json.example -> appsettings.Local.json in DIR
and set PostgreSql.enabled=true plus a real DSN. Do not enable MariaDb.

Stop path: write FROG_SHUTDOWN_FILE (same as integration tests), then SIGTERM.
Ctrl+C / SIGTERM is also supported when running --foreground.
'

die() {
  echo "error: $*" >&2
  exit 1
}

ACTION=""
DIR=""
FOREGROUND=0
PORT=""
BIND=""
TIMEOUT=20

while [[ $# -gt 0 ]]; do
  case "$1" in
    start|stop) ACTION="$1"; shift ;;
    --dir) DIR="${2:-}"; shift 2 ;;
    --foreground) FOREGROUND=1; shift ;;
    --port) PORT="${2:-}"; shift 2 ;;
    --bind) BIND="${2:-}"; shift 2 ;;
    --timeout) TIMEOUT="${2:-}"; shift 2 ;;
    -h|--help) printf '%s' "$USAGE"; exit 0 ;;
    *) die "unknown argument: $1" ;;
  esac
done

[[ -n "$ACTION" ]] || die "pass start or stop"
[[ -n "$DIR" ]] || die "--dir is required"
DIR="$(cd "$DIR" && pwd)"

PID_FILE="${DIR}/frog-server.pid"
LOG_FILE="${DIR}/frog-server.log"
SHUTDOWN_FILE="${FROG_SHUTDOWN_FILE:-${DIR}/.frog-shutdown-request}"

resolve_server_cmd() {
  if [[ -x "${DIR}/Frog.Server" ]]; then
    SERVER_BIN="${DIR}/Frog.Server"
    SERVER_PREFIX=()
  elif [[ -f "${DIR}/Frog.Server.dll" ]]; then
    command -v dotnet >/dev/null 2>&1 || die "dotnet not on PATH (framework-dependent publish)"
    SERVER_BIN="dotnet"
    SERVER_PREFIX=("${DIR}/Frog.Server.dll")
  else
    die "no Frog.Server host in ${DIR} (expected Frog.Server or Frog.Server.dll)"
  fi
}

assert_overlay() {
  [[ -f "${DIR}/appsettings.json" ]] || die "appsettings.json missing in ${DIR} (wrong directory?)"
  if [[ ! -f "${DIR}/appsettings.Local.json" && -z "${FROG_POSTGRES_CONNECTION_STRING:-}" ]]; then
    die "copy ${DIR}/appsettings.Local.json.example to appsettings.Local.json (or set FROG_POSTGRES_CONNECTION_STRING)"
  fi
}

start_foreground() {
  assert_overlay
  resolve_server_cmd
  rm -f "$SHUTDOWN_FILE"
  export FROG_SHUTDOWN_FILE="$SHUTDOWN_FILE"
  cd "$DIR"
  extra=()
  [[ -n "$PORT" ]] && extra+=(--Server:Port="$PORT")
  [[ -n "$BIND" ]] && extra+=(--Server:BindAddress="$BIND")
  exec "$SERVER_BIN" "${SERVER_PREFIX[@]}" --contentRoot "$DIR" "${extra[@]}"
}

start_background() {
  assert_overlay
  resolve_server_cmd
  if [[ -f "$PID_FILE" ]] && kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
    die "already running (pid $(cat "$PID_FILE")); stop first"
  fi
  rm -f "$SHUTDOWN_FILE"
  extra=()
  [[ -n "$PORT" ]] && extra+=(--Server:Port="$PORT")
  [[ -n "$BIND" ]] && extra+=(--Server:BindAddress="$BIND")
  (
    cd "$DIR"
    export FROG_SHUTDOWN_FILE="$SHUTDOWN_FILE"
    exec "$SERVER_BIN" "${SERVER_PREFIX[@]}" --contentRoot "$DIR" "${extra[@]}"
  ) >>"$LOG_FILE" 2>&1 &
  echo $! >"$PID_FILE"
  echo "started pid $(cat "$PID_FILE") log ${LOG_FILE}"
}

stop_server() {
  local pid=""
  if [[ -f "$PID_FILE" ]]; then
    pid="$(cat "$PID_FILE")"
  fi
  mkdir -p "$(dirname "$SHUTDOWN_FILE")"
  : >"$SHUTDOWN_FILE"
  if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
    kill -TERM "$pid" 2>/dev/null || true
  fi
  local waited=0
  while [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; do
    if (( waited >= TIMEOUT )); then
      echo "error: process ${pid} still running after ${TIMEOUT}s (wrote ${SHUTDOWN_FILE}; sent SIGTERM)" >&2
      exit 1
    fi
    sleep 1
    waited=$((waited + 1))
  done
  rm -f "$PID_FILE"
  echo "stopped"
}

case "$ACTION" in
  start)
    if [[ "$FOREGROUND" -eq 1 ]]; then
      start_foreground
    else
      start_background
    fi
    ;;
  stop)
    stop_server
    ;;
esac
