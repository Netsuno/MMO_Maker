#!/usr/bin/env bash
# Operator / agent smoke for the P9-4 publish layout.
# Always publishes server-linux-x64 and verifies files.
# When FROG_POSTGRES_TEST_CONNECTION_STRING (or FROG_POSTGRES_CONNECTION_STRING)
# is set, also runs PackagedServerPostgreSqlProcessTests (seeded world + TCP).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LAYOUT_ONLY=0
FORCE=1

USAGE='Usage: packaged-server-smoke.sh [--layout-only]

  1) ./scripts/publish-frog.sh --target server-linux-x64 --force
  2) If a PostgreSQL admin DSN is in the environment, run
     PackagedServerPostgreSqlProcessTests (packaged process + PG + graceful stop).

Client/editor launch is gated by existing Windows CI smokes — this script does
not claim WinForms proof on Linux.
'

while [[ $# -gt 0 ]]; do
  case "$1" in
    --layout-only) LAYOUT_ONLY=1; shift ;;
    -h|--help) printf '%s' "$USAGE"; exit 0 ;;
    *) echo "error: unknown argument: $1" >&2; exit 1 ;;
  esac
done

"${ROOT}/scripts/publish-frog.sh" --target server-linux-x64 --force

if [[ "$LAYOUT_ONLY" -eq 1 ]]; then
  echo "layout-only smoke OK"
  exit 0
fi

DSN="${FROG_POSTGRES_TEST_CONNECTION_STRING:-${FROG_POSTGRES_CONNECTION_STRING:-}}"
if [[ -z "$DSN" ]]; then
  echo "skip process start: set FROG_POSTGRES_TEST_CONNECTION_STRING to run PackagedServerPostgreSqlProcessTests"
  exit 0
fi

export FROG_POSTGRES_TEST_CONNECTION_STRING="$DSN"
dotnet test "${ROOT}/tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj" \
  -c Release \
  --filter "FullyQualifiedName~.PackagedServerPostgreSqlProcessTests" \
  --verbosity normal
