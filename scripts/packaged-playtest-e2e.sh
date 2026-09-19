#!/usr/bin/env bash
# P10-3: playtest from a published zip extracted *outside* the git tree.
# Linux cannot launch WinForms Frog.Client.exe / Frog.Editor.exe (not a pass).
# This script proves the delivered *server* zip: extract outside the repo,
# start Frog.Server, TCP Hello, then graceful stop.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SKIP_PUBLISH=0
PUBLISH_ROOT="${ROOT}/artifacts/publish"
OUTSIDE=""
PORT=""

USAGE='Usage: packaged-playtest-e2e.sh [options]

Linux: packaged server TCP playtest from zip outside the repo.
WinForms editor/client playtest: not proven on Linux agents.

Options:
  --skip-publish       reuse artifacts/publish/archives/server-linux-x64.zip
  --publish-root DIR
  --outside-root DIR   must be outside the git tree
  --port N
'

while [[ $# -gt 0 ]]; do
  case "$1" in
    --skip-publish) SKIP_PUBLISH=1; shift ;;
    --publish-root) PUBLISH_ROOT="${2:-}"; shift 2 ;;
    --outside-root) OUTSIDE="${2:-}"; shift 2 ;;
    --port) PORT="${2:-}"; shift 2 ;;
    -h|--help) printf '%s' "$USAGE"; exit 0 ;;
    *) echo "error: unknown argument: $1" >&2; exit 2 ;;
  esac
done

if [[ "$SKIP_PUBLISH" -eq 0 ]]; then
  "${ROOT}/scripts/publish-frog.sh" --target server-linux-x64 --output-root "$PUBLISH_ROOT" --force
fi

ZIP="${PUBLISH_ROOT}/archives/server-linux-x64.zip"
[[ -f "$ZIP" ]] || { echo "error: missing $ZIP" >&2; exit 1; }

if [[ -z "$OUTSIDE" ]]; then
  OUTSIDE="$(mktemp -d /tmp/frog-p10-3-outside-XXXXXX)"
fi
mkdir -p "$OUTSIDE"
OUTSIDE="$(cd "$OUTSIDE" && pwd)"
REPO_ABS="$(cd "$ROOT" && pwd)"
case "$OUTSIDE" in
  "$REPO_ABS"|"$REPO_ABS"/*)
    echo "error: outside-root must be outside the git tree, got $OUTSIDE" >&2
    exit 1
    ;;
esac

DEST="${OUTSIDE}/extracted"
rm -rf "$DEST"
mkdir -p "$DEST"
ZIP="$ZIP" DEST="$DEST" python3 -c 'import os, zipfile; zipfile.ZipFile(os.environ["ZIP"]).extractall(os.environ["DEST"])'
# CPython zipfile.extractall does not restore Unix execute bits.
if [[ -f "${DEST}/server-linux-x64/Frog.Server" ]]; then
  SERVER_DIR="${DEST}/server-linux-x64"
elif [[ -f "${DEST}/Frog.Server" ]]; then
  SERVER_DIR="$DEST"
else
  echo "error: Frog.Server missing under $DEST" >&2
  find "$DEST" -maxdepth 3 -type f | head
  exit 1
fi
chmod +x "${SERVER_DIR}/Frog.Server"

if [[ -z "$PORT" ]]; then
  PORT="$(python3 -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1",0)); print(s.getsockname()[1]); s.close()')"
fi

cat > "${SERVER_DIR}/appsettings.Local.json" <<JSON
{
  "Server": { "Port": ${PORT}, "BindAddress": "127.0.0.1" },
  "MariaDb": { "Enabled": false },
  "PostgreSql": { "Enabled": false, "AllowInMemoryFallback": true }
}
JSON

SHUTDOWN="${SERVER_DIR}/.frog-shutdown-request"
rm -f "$SHUTDOWN"
export FROG_SHUTDOWN_FILE="$SHUTDOWN"

echo "==> start packaged Frog.Server from $SERVER_DIR port $PORT"
"${SERVER_DIR}/Frog.Server" --contentRoot "${SERVER_DIR}" \
  >"${OUTSIDE}/server.stdout" 2>"${OUTSIDE}/server.stderr" &
SERVER_PID=$!
cleanup() {
  if kill -0 "$SERVER_PID" 2>/dev/null; then
    : > "$SHUTDOWN" || true
    kill -TERM "$SERVER_PID" 2>/dev/null || true
    wait "$SERVER_PID" 2>/dev/null || true
  fi
}
trap cleanup EXIT

python3 - "$PORT" <<'PY'
import socket, struct, sys, time
port = int(sys.argv[1])
deadline = time.time() + 25
sock = None
while time.time() < deadline:
    try:
        sock = socket.create_connection(("127.0.0.1", port), timeout=1)
        break
    except OSError:
        time.sleep(0.2)
if sock is None:
    raise SystemExit("server did not accept TCP")

def read_frame(s, timeout=8):
    s.settimeout(timeout)
    hdr = b""
    while len(hdr) < 4:
        chunk = s.recv(4 - len(hdr))
        if not chunk:
            raise EOFError("eof header")
        hdr += chunk
    n = struct.unpack("<i", hdr)[0]
    body = b""
    while len(body) < n:
        chunk = s.recv(n - len(body))
        if not chunk:
            raise EOFError("eof body")
        body += chunk
    return body

hello = read_frame(sock)
if hello[:1] != b"\x01":
    raise SystemExit("expected Hello, got %r" % (hello[:1],))
print("OK Hello from packaged zip server")
sock.close()
print("PROUVE: playtest TCP depuis zip serveur extrait hors depot")
print("LINUX: WinForms Frog.Client.exe / Frog.Editor.exe playtest = not proven")
PY

echo "OK packaged playtest E2E (server zip outside repo)"
echo "outside=$OUTSIDE"
echo "WINFORMS: not proven on Linux agents"
