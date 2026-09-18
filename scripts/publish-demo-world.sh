#!/usr/bin/env bash
# Publie le monde démo P10-4 sur PostgreSQL (migrate + Save/Publish).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DSN="${FROG_POSTGRES_CONNECTION_STRING:-${FROG_POSTGRES_TEST_CONNECTION_STRING:-}}"
if [[ $# -ge 1 && "$1" == "--connection-string" ]]; then
  DSN="${2:-}"
fi
if [[ -z "$DSN" ]]; then
  echo "error: set FROG_POSTGRES_CONNECTION_STRING or pass --connection-string" >&2
  exit 2
fi
dotnet run --project "${ROOT}/tools/Frog.DemoWorld/Frog.DemoWorld.csproj" -c Release --no-launch-profile -- publish --connection-string "$DSN"
