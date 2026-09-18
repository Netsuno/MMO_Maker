#!/usr/bin/env bash
# Restore a Frog pg_dump custom-format backup into an empty (or recreated) database.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=postgres-common.sh
source "${ROOT}/scripts/postgres-common.sh"

USAGE='Usage: postgres-restore.sh --input FILE [options]

Restore a custom-format dump produced by postgres-backup.sh into a PostgreSQL
database that does not already contain Frog schemas.

Do NOT run EF Database.Migrate() on the target before restore (duplicate objects).
After restore, Database.Migrate() must apply 0 pending migrations.

Options:
  --input FILE                 Source .dump path (required)
  --connection STRING          Target Npgsql or libpq connection string
  --host HOST --port PORT --user USER --password PASS --database NAME
  --maintenance-database NAME  Database used for CREATE/DROP (default: postgres)
  --create-database            CREATE DATABASE if the target name does not exist
  --recreate                   Terminate sessions, DROP DATABASE, CREATE DATABASE
                               (destructive; disposable targets only)
  --skip-verify                Do not run schema/history checks after restore
  --verbose                    Pass --verbose to pg_restore
  -h, --help                   Show this help
'

INPUT=""
CONNECTION="${FROG_POSTGRES_CONNECTION_STRING:-${FROG_POSTGRES_TEST_CONNECTION_STRING:-}}"
MAINT="postgres"
CREATE_DB=0
RECREATE=0
SKIP_VERIFY=0
VERBOSE=0
HOST_FLAG=""
PORT_FLAG=""
USER_FLAG=""
PASSWORD_FLAG=""
DATABASE_FLAG=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --input) INPUT="${2:-}"; shift 2 ;;
    --connection) CONNECTION="${2:-}"; shift 2 ;;
    --host) HOST_FLAG="${2:-}"; shift 2 ;;
    --port) PORT_FLAG="${2:-}"; shift 2 ;;
    --user) USER_FLAG="${2:-}"; shift 2 ;;
    --password) PASSWORD_FLAG="${2:-}"; shift 2 ;;
    --database) DATABASE_FLAG="${2:-}"; shift 2 ;;
    --maintenance-database) MAINT="${2:-}"; shift 2 ;;
    --create-database) CREATE_DB=1; shift ;;
    --recreate) RECREATE=1; CREATE_DB=1; shift ;;
    --skip-verify) SKIP_VERIFY=1; shift ;;
    --verbose) VERBOSE=1; shift ;;
    -h|--help) printf '%s' "$USAGE"; exit 0 ;;
    *) frog_pg_die "unknown argument: $1" ;;
  esac
done

[[ -n "$INPUT" ]] || frog_pg_die "--input is required"
[[ -f "$INPUT" ]] || frog_pg_die "input dump not found: $INPUT"

frog_pg_require_cmd pg_restore
frog_pg_require_cmd psql
frog_pg_parse_connection "$CONNECTION"
[[ -n "$HOST_FLAG" ]] && FROG_PG_HOST="$HOST_FLAG"
[[ -n "$PORT_FLAG" ]] && FROG_PG_PORT="$PORT_FLAG"
[[ -n "$USER_FLAG" ]] && FROG_PG_USER="$USER_FLAG"
[[ -n "$PASSWORD_FLAG" ]] && FROG_PG_PASSWORD="$PASSWORD_FLAG"
[[ -n "$DATABASE_FLAG" ]] && FROG_PG_DATABASE="$DATABASE_FLAG"
frog_pg_require_target

MAINT="$(frog_pg_trim "$MAINT")"
[[ -n "$MAINT" ]] || frog_pg_die "--maintenance-database must not be empty"
TARGET="$(frog_pg_quoted_ident "$FROG_PG_DATABASE")"
MAINT_Q="$(frog_pg_quoted_ident "$MAINT")"

db_exists() {
  local name="$1"
  local found
  found="$(frog_pg_psql_db "$MAINT" -tAc "SELECT 1 FROM pg_database WHERE datname = '${name}'" | frog_pg_trim)"
  [[ "$found" == "1" ]]
}

if [[ "$RECREATE" -eq 1 ]]; then
  [[ "$TARGET" != "$MAINT_Q" ]] || frog_pg_die "refusing to DROP the maintenance database ($TARGET)"
  echo "recreate: terminating sessions and dropping ${TARGET}"
  frog_pg_psql_db "$MAINT" -c \
    "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '${TARGET}' AND pid <> pg_backend_pid();" \
    >/dev/null
  frog_pg_psql_db "$MAINT" -c "DROP DATABASE IF EXISTS ${TARGET};"
fi

if ! db_exists "$TARGET"; then
  if [[ "$CREATE_DB" -eq 1 || "$RECREATE" -eq 1 ]]; then
    echo "creating database ${TARGET}"
    frog_pg_psql_db "$MAINT" -c "CREATE DATABASE ${TARGET} OWNER ${FROG_PG_USER};" \
      || frog_pg_psql_db "$MAINT" -c "CREATE DATABASE ${TARGET};"
  else
    frog_pg_die "target database ${TARGET} does not exist (pass --create-database or --recreate)"
  fi
fi

existing="$(frog_pg_schema_count "$TARGET" | frog_pg_trim)"
if [[ "$existing" != "0" ]]; then
  frog_pg_die "target ${TARGET} already has ${existing} Frog schema(s). Restore onto an empty database, or pass --recreate."
fi

frog_pg_export_libpq "$TARGET"
RESTORE_ARGS=(
  --no-owner
  --no-acl
  --exit-on-error
  --single-transaction
  --dbname="$TARGET"
)
if [[ "$VERBOSE" -eq 1 ]]; then
  RESTORE_ARGS+=(--verbose)
fi

echo "restoring ${INPUT} -> ${TARGET}@${FROG_PG_HOST}:${FROG_PG_PORT}"
pg_restore "${RESTORE_ARGS[@]}" "$INPUT"
echo "restore ok: ${TARGET}"

if [[ "$SKIP_VERIFY" -ne 1 ]]; then
  bash "${ROOT}/scripts/postgres-verify.sh" \
    --host "$FROG_PG_HOST" \
    --port "$FROG_PG_PORT" \
    --user "$FROG_PG_USER" \
    --password "$FROG_PG_PASSWORD" \
    --database "$TARGET"
fi
