#!/usr/bin/env bash
# Create frog_runtime / frog_migrate / frog_publish (+ frog_ops recommended)
# and apply DML-only grants for the runtime role.
# Requires a superuser (or CREATEROLE + GRANT option) connection — not frog_runtime.
# docker-compose demo (POSTGRES_USER=frog superuser) is NOT the hosted profile.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=postgres-common.sh
source "${ROOT}/scripts/postgres-common.sh"

USAGE='Usage: postgres-apply-roles.sh [options]

Creates LOGIN roles frog_runtime, frog_migrate, frog_publish, frog_ops
(NOSUPERUSER NOCREATEDB NOCREATEROLE) and grants DML (no DDL) to frog_runtime
on schemas auth, content, ops, player, world.

Passwords (required unless --skip-passwords):
  FROG_PG_RUNTIME_PASSWORD
  FROG_PG_MIGRATE_PASSWORD
  FROG_PG_PUBLISH_PASSWORD
  FROG_PG_OPS_PASSWORD          optional; role still created, password left unset if empty

Options:
  --connection STRING      Npgsql or libpq connection string (admin)
  --host HOST              Default: 127.0.0.1 or PGHOST
  --port PORT              Default: 5432
  --user USER              Admin role (must be able to CREATE ROLE)
  --password PASS
  --database NAME
  --skip-passwords         Create/alter roles without ALTER PASSWORD
  --skip-ops               Do not grant frog_ops (role is still created in postgres-roles.sql)
  -h, --help

See docs/progress/phase-10-beta-release/POSTGRES_ROLES.md
'

CONNECTION="${FROG_POSTGRES_CONNECTION_STRING:-${FROG_POSTGRES_TEST_CONNECTION_STRING:-}}"
HOST_FLAG=""
PORT_FLAG=""
USER_FLAG=""
PASSWORD_FLAG=""
DATABASE_FLAG=""
SKIP_PASSWORDS=0
SKIP_OPS=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --connection) CONNECTION="${2:-}"; shift 2 ;;
    --host) HOST_FLAG="${2:-}"; shift 2 ;;
    --port) PORT_FLAG="${2:-}"; shift 2 ;;
    --user) USER_FLAG="${2:-}"; shift 2 ;;
    --password) PASSWORD_FLAG="${2:-}"; shift 2 ;;
    --database) DATABASE_FLAG="${2:-}"; shift 2 ;;
    --skip-passwords) SKIP_PASSWORDS=1; shift ;;
    --skip-ops) SKIP_OPS=1; shift ;;
    -h|--help) printf '%s' "$USAGE"; exit 0 ;;
    *) frog_pg_die "unknown argument: $1" ;;
  esac
done

frog_pg_require_cmd psql
frog_pg_parse_connection "$CONNECTION"
[[ -n "$HOST_FLAG" ]] && FROG_PG_HOST="$HOST_FLAG"
[[ -n "$PORT_FLAG" ]] && FROG_PG_PORT="$PORT_FLAG"
[[ -n "$USER_FLAG" ]] && FROG_PG_USER="$USER_FLAG"
[[ -n "$PASSWORD_FLAG" ]] && FROG_PG_PASSWORD="$PASSWORD_FLAG"
[[ -n "$DATABASE_FLAG" ]] && FROG_PG_DATABASE="$DATABASE_FLAG"
frog_pg_require_target

if [[ "$SKIP_PASSWORDS" -ne 1 ]]; then
  [[ -n "${FROG_PG_RUNTIME_PASSWORD:-}" ]] || frog_pg_die "FROG_PG_RUNTIME_PASSWORD is required (or pass --skip-passwords)"
  [[ -n "${FROG_PG_MIGRATE_PASSWORD:-}" ]] || frog_pg_die "FROG_PG_MIGRATE_PASSWORD is required (or pass --skip-passwords)"
  [[ -n "${FROG_PG_PUBLISH_PASSWORD:-}" ]] || frog_pg_die "FROG_PG_PUBLISH_PASSWORD is required (or pass --skip-passwords)"
fi

frog_pg_export_libpq

frog_pg_psql_db "$FROG_PG_DATABASE" -f "${ROOT}/scripts/postgres-roles.sql"

set_password() {
  local role="$1"
  local pw="$2"
  [[ -n "$pw" ]] || return 0
  frog_pg_psql_db "$FROG_PG_DATABASE" \
    -v pw="$pw" \
    -c "ALTER ROLE ${role} PASSWORD :'pw';"
}

if [[ "$SKIP_PASSWORDS" -ne 1 ]]; then
  set_password frog_runtime "$FROG_PG_RUNTIME_PASSWORD"
  set_password frog_migrate "$FROG_PG_MIGRATE_PASSWORD"
  set_password frog_publish "$FROG_PG_PUBLISH_PASSWORD"
  if [[ -n "${FROG_PG_OPS_PASSWORD:-}" ]]; then
    set_password frog_ops "$FROG_PG_OPS_PASSWORD"
  fi
fi

OPS_GUC="frog_ops"
if [[ "$SKIP_OPS" -eq 1 ]]; then
  OPS_GUC=""
fi

# Same session as the grants file so custom GUCs are visible to the DO block.
frog_pg_psql_db "$FROG_PG_DATABASE" <<SQL
SELECT set_config('frog.runtime_role', 'frog_runtime', false);
SELECT set_config('frog.migrate_role', 'frog_migrate', false);
SELECT set_config('frog.publish_role', 'frog_publish', false);
SELECT set_config('frog.ops_role', '${OPS_GUC}', false);
\\i ${ROOT}/scripts/postgres-role-grants.sql
SQL

echo "applied least-privilege roles on ${FROG_PG_DATABASE} (runtime=frog_runtime, not superuser)"
