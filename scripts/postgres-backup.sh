#!/usr/bin/env bash
# Dump a Frog PostgreSQL database (custom format) for later pg_restore.
# Dumps product schemas auth, content, ops, player, world (includes ops.legacy_imports
# and world.__EFMigrationsHistory). Does not dump cluster roles.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=postgres-common.sh
source "${ROOT}/scripts/postgres-common.sh"

USAGE='Usage: postgres-backup.sh --output FILE [options]

Dump the Frog EF schema (auth, content, ops, player, world) with pg_dump
custom format (-Fc). Restore with postgres-restore.sh.

Options:
  --output FILE            Destination .dump path (required)
  --connection STRING      Npgsql or libpq connection string
  --host HOST              Default: 127.0.0.1 or PGHOST
  --port PORT              Default: 5432
  --user USER              Role used to dump
  --password PASS          Prefer env PGPASSWORD; this flag exports it
  --database NAME          Database to dump
  --force                  Overwrite existing --output
  --verbose                Pass --verbose to pg_dump
  -h, --help               Show this help

Environment: PGHOST PGPORT PGUSER PGPASSWORD PGDATABASE PGSSLMODE
Also accepts FROG_POSTGRES_TEST_CONNECTION_STRING / FROG_POSTGRES_CONNECTION_STRING
when --connection is omitted.
'

OUTPUT=""
CONNECTION="${FROG_POSTGRES_CONNECTION_STRING:-${FROG_POSTGRES_TEST_CONNECTION_STRING:-}}"
FORCE=0
VERBOSE=0
HOST_FLAG=""
PORT_FLAG=""
USER_FLAG=""
PASSWORD_FLAG=""
DATABASE_FLAG=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --output) OUTPUT="${2:-}"; shift 2 ;;
    --connection) CONNECTION="${2:-}"; shift 2 ;;
    --host) HOST_FLAG="${2:-}"; shift 2 ;;
    --port) PORT_FLAG="${2:-}"; shift 2 ;;
    --user) USER_FLAG="${2:-}"; shift 2 ;;
    --password) PASSWORD_FLAG="${2:-}"; shift 2 ;;
    --database) DATABASE_FLAG="${2:-}"; shift 2 ;;
    --force) FORCE=1; shift ;;
    --verbose) VERBOSE=1; shift ;;
    -h|--help) printf '%s' "$USAGE"; exit 0 ;;
    *) frog_pg_die "unknown argument: $1" ;;
  esac
done

[[ -n "$OUTPUT" ]] || frog_pg_die "--output is required"

frog_pg_require_cmd pg_dump
frog_pg_parse_connection "$CONNECTION"
[[ -n "$HOST_FLAG" ]] && FROG_PG_HOST="$HOST_FLAG"
[[ -n "$PORT_FLAG" ]] && FROG_PG_PORT="$PORT_FLAG"
[[ -n "$USER_FLAG" ]] && FROG_PG_USER="$USER_FLAG"
[[ -n "$PASSWORD_FLAG" ]] && FROG_PG_PASSWORD="$PASSWORD_FLAG"
[[ -n "$DATABASE_FLAG" ]] && FROG_PG_DATABASE="$DATABASE_FLAG"
frog_pg_require_target

if [[ -e "$OUTPUT" && "$FORCE" -ne 1 ]]; then
  frog_pg_die "output exists (pass --force to overwrite): $OUTPUT"
fi

mkdir -p "$(dirname "$OUTPUT")"

frog_pg_export_libpq
DUMP_ARGS=(
  --format=custom
  --compress=9
  --no-owner
  --no-acl
  --file="$OUTPUT"
)
while IFS= read -r schema_arg; do
  [[ -n "$schema_arg" ]] && DUMP_ARGS+=("$schema_arg")
done < <(frog_pg_schema_dump_args)

if [[ "$VERBOSE" -eq 1 ]]; then
  DUMP_ARGS+=(--verbose)
fi

echo "dumping ${FROG_PG_DATABASE}@${FROG_PG_HOST}:${FROG_PG_PORT} (schemas: ${FROG_PG_SCHEMAS[*]}) -> ${OUTPUT}"
pg_dump "${DUMP_ARGS[@]}" "$FROG_PG_DATABASE"
echo "backup ok: $OUTPUT ($(wc -c < "$OUTPUT") bytes)"
