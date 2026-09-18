#!/usr/bin/env bash
# P9-3 proof path: either the CI integration test, or a disposable dump→restore roundtrip
# of an existing Frog database.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=postgres-common.sh
source "${ROOT}/scripts/postgres-common.sh"

USAGE='Usage: postgres-backup-restore-smoke.sh [mode] [options]

Modes:
  --integration-test   (default) Run PostgresBackupRestoreTests against
                       FROG_POSTGRES_TEST_CONNECTION_STRING.
  --roundtrip          Dump --connection (or FROG_POSTGRES_CONNECTION_STRING),
                       restore into a new disposable database, verify schemas,
                       drop the copy unless --keep.

Options:
  --connection STRING  Source (roundtrip) or admin (integration uses the env var)
  --configuration CFG  dotnet configuration (default: Release)
  --keep               Roundtrip: do not DROP the restored copy
  --output FILE        Roundtrip dump path (default: temp file)
  -h, --help
'

MODE="integration-test"
CONNECTION="${FROG_POSTGRES_CONNECTION_STRING:-${FROG_POSTGRES_TEST_CONNECTION_STRING:-}}"
CONFIGURATION="Release"
KEEP=0
OUTPUT=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --integration-test) MODE="integration-test"; shift ;;
    --roundtrip) MODE="roundtrip"; shift ;;
    --connection) CONNECTION="${2:-}"; shift 2 ;;
    --configuration) CONFIGURATION="${2:-}"; shift 2 ;;
    --keep) KEEP=1; shift ;;
    --output) OUTPUT="${2:-}"; shift 2 ;;
    -h|--help) printf '%s' "$USAGE"; exit 0 ;;
    *) frog_pg_die "unknown argument: $1" ;;
  esac
done

frog_pg_require_cmd pg_dump
frog_pg_require_cmd pg_restore
frog_pg_require_cmd psql

if [[ "$MODE" == "integration-test" ]]; then
  frog_pg_require_cmd dotnet
  if [[ -z "${FROG_POSTGRES_TEST_CONNECTION_STRING:-}" ]]; then
    if [[ -n "$CONNECTION" ]]; then
      export FROG_POSTGRES_TEST_CONNECTION_STRING="$CONNECTION"
    else
      frog_pg_die "set FROG_POSTGRES_TEST_CONNECTION_STRING (admin DB with CREATEDB)"
    fi
  fi
  echo "running PostgresBackupRestoreTests (configuration=${CONFIGURATION})"
  exec dotnet test \
    "${ROOT}/tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj" \
    -c "$CONFIGURATION" \
    --filter "FullyQualifiedName~.PostgresBackupRestoreTests" \
    --verbosity normal
fi

[[ -n "$CONNECTION" ]] || frog_pg_die "--roundtrip requires --connection or FROG_POSTGRES_CONNECTION_STRING"
frog_pg_parse_connection "$CONNECTION"
frog_pg_require_target
SOURCE_DB="$FROG_PG_DATABASE"
SOURCE_CONN="$CONNECTION"

if [[ -z "$OUTPUT" ]]; then
  OUTPUT="$(mktemp "${TMPDIR:-/tmp}/frog-p93-XXXXXX.dump")"
fi

bash "${ROOT}/scripts/postgres-backup.sh" --connection "$SOURCE_CONN" --output "$OUTPUT" --force

stamp="$(date +%Y%m%d%H%M%S)"
dest_name="frog_p93_rt_${stamp}"
dest_name="$(frog_pg_quoted_ident "$dest_name")"

# Rewrite Database= in an Npgsql string; fall back to flag form.
if [[ "$SOURCE_CONN" == *";"* || "$SOURCE_CONN" == *"Database="* || "$SOURCE_CONN" == *"database="* ]]; then
  dest_conn="$(printf '%s' "$SOURCE_CONN" | sed -E 's/[Dd]atabase=[^;]*/Database='"$dest_name"'/')"
  if [[ "$dest_conn" == "$SOURCE_CONN" ]]; then
    dest_conn="${SOURCE_CONN%;};Database=${dest_name}"
  fi
else
  dest_conn="$SOURCE_CONN"
fi

MAINT="$SOURCE_DB"
echo "roundtrip restore into disposable database ${dest_name} (maintenance=${MAINT})"
bash "${ROOT}/scripts/postgres-restore.sh" \
  --connection "$dest_conn" \
  --database "$dest_name" \
  --host "$FROG_PG_HOST" \
  --port "$FROG_PG_PORT" \
  --user "$FROG_PG_USER" \
  --password "$FROG_PG_PASSWORD" \
  --maintenance-database "$MAINT" \
  --create-database \
  --input "$OUTPUT"

echo "roundtrip verify passed for ${dest_name}"
echo "PostgresDatabaseHealth / login proof: run --integration-test (uses EF + Phase 7 TCP)."

if [[ "$KEEP" -eq 1 ]]; then
  echo "kept restored copy ${dest_name} (dump ${OUTPUT})"
  exit 0
fi

echo "dropping disposable ${dest_name}"
frog_pg_parse_connection "$SOURCE_CONN"
frog_pg_psql_db "$MAINT" -c \
  "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '${dest_name}' AND pid <> pg_backend_pid();" \
  >/dev/null || true
frog_pg_psql_db "$MAINT" -c "DROP DATABASE IF EXISTS ${dest_name};"
rm -f "$OUTPUT"
echo "roundtrip smoke ok"
