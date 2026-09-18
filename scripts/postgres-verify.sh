#!/usr/bin/env bash
# Lightweight SQL verification that a Frog database has the expected schemas
# and EF migrations history. Pending-migration "health OK" is PostgresDatabaseHealth
# (C# / server start / integration test) — this script cannot see EF's assembly list.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=postgres-common.sh
source "${ROOT}/scripts/postgres-common.sh"

USAGE='Usage: postgres-verify.sh [options]

Checks:
  - TCP/libpq connection
  - schemas auth, content, ops, player, world all exist
  - __EFMigrationsHistory exists and has at least one row

Does not replace PostgresDatabaseHealth (pending migrations vs the running assembly).

Options:
  --connection STRING
  --host HOST --port PORT --user USER --password PASS --database NAME
  -h, --help
'

CONNECTION="${FROG_POSTGRES_CONNECTION_STRING:-${FROG_POSTGRES_TEST_CONNECTION_STRING:-}}"
HOST_FLAG=""
PORT_FLAG=""
USER_FLAG=""
PASSWORD_FLAG=""
DATABASE_FLAG=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --connection) CONNECTION="${2:-}"; shift 2 ;;
    --host) HOST_FLAG="${2:-}"; shift 2 ;;
    --port) PORT_FLAG="${2:-}"; shift 2 ;;
    --user) USER_FLAG="${2:-}"; shift 2 ;;
    --password) PASSWORD_FLAG="${2:-}"; shift 2 ;;
    --database) DATABASE_FLAG="${2:-}"; shift 2 ;;
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

echo "verify ${FROG_PG_DATABASE}@${FROG_PG_HOST}:${FROG_PG_PORT}"

names="$(frog_pg_psql_db "$FROG_PG_DATABASE" -tAc \
  "SELECT string_agg(nspname, ',' ORDER BY nspname)
   FROM pg_namespace
   WHERE nspname IN ('auth','content','ops','player','world');" | frog_pg_trim)"
expected="auth,content,ops,player,world"
[[ "$names" == "$expected" ]] || frog_pg_die "expected schemas ${expected}; got: ${names:-none}"

hist="$(frog_pg_history_relation "$FROG_PG_DATABASE" | frog_pg_trim)"
[[ -n "$hist" ]] || frog_pg_die "__EFMigrationsHistory not found (dump missing EF history?)"

count="$(frog_pg_psql_db "$FROG_PG_DATABASE" -tAc "SELECT COUNT(*) FROM ${hist};" | frog_pg_trim)"
[[ "$count" =~ ^[0-9]+$ && "$count" -ge 1 ]] || frog_pg_die "EF history ${hist} has no rows"

echo "verify ok: schemas=${names} history=${hist} rows=${count}"
echo "next: PostgresDatabaseHealth must report 0 pending (server start or integration test)."
