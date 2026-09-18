#!/usr/bin/env bash
# Shared helpers for Frog PostgreSQL backup / restore / verify scripts.
# Product schemas (FrogDbContext): auth, content, ops, player, world.
# shellcheck disable=SC2034

FROG_PG_SCHEMAS=(auth content ops player world)

frog_pg_die() {
  echo "error: $*" >&2
  exit 1
}

frog_pg_require_cmd() {
  command -v "$1" >/dev/null 2>&1 || frog_pg_die "required command not found: $1 (install postgresql-client-16)"
}

frog_pg_trim() {
  local s="$1"
  s="${s#"${s%%[![:space:]]*}"}"
  s="${s%"${s##*[![:space:]]}"}"
  printf '%s' "$s"
}

# Parse an Npgsql ("Host=...;Database=...") or libpq ("host=... dbname=...") string.
# Individual --host/--port/--user/--password/--database flags should be applied after this.
frog_pg_parse_connection() {
  local cs="${1:-}"
  FROG_PG_HOST="${PGHOST:-127.0.0.1}"
  FROG_PG_PORT="${PGPORT:-5432}"
  FROG_PG_USER="${PGUSER:-}"
  FROG_PG_PASSWORD="${PGPASSWORD:-}"
  FROG_PG_DATABASE="${PGDATABASE:-}"

  [[ -z "$cs" ]] && return 0

  local -a parts=()
  local part key value key_lc
  if [[ "$cs" == *";"* ]]; then
    IFS=';' read -ra parts <<< "$cs"
  else
    # libpq keyword/value: host=127.0.0.1 port=5432 dbname=frog
    read -ra parts <<< "$cs"
  fi

  for part in "${parts[@]}"; do
    part="$(frog_pg_trim "$part")"
    [[ -z "$part" ]] && continue
    [[ "$part" != *"="* ]] && continue
    key="$(frog_pg_trim "${part%%=*}")"
    value="${part#*=}"
    key_lc="$(printf '%s' "$key" | tr '[:upper:]' '[:lower:]' | tr -d '[:space:]')"
    case "$key_lc" in
      host|server) FROG_PG_HOST="$value" ;;
      port) FROG_PG_PORT="$value" ;;
      database|db|dbname) FROG_PG_DATABASE="$value" ;;
      username|user|userid|uid) FROG_PG_USER="$value" ;;
      password|pwd) FROG_PG_PASSWORD="$value" ;;
    esac
  done
}

frog_pg_require_target() {
  [[ -n "${FROG_PG_HOST:-}" ]] || frog_pg_die "host is required (--host or --connection)"
  [[ -n "${FROG_PG_PORT:-}" ]] || frog_pg_die "port is required (--port or --connection)"
  [[ -n "${FROG_PG_USER:-}" ]] || frog_pg_die "user is required (--user or --connection)"
  [[ -n "${FROG_PG_DATABASE:-}" ]] || frog_pg_die "database is required (--database or --connection)"
}

frog_pg_export_libpq() {
  export PGHOST="$FROG_PG_HOST"
  export PGPORT="$FROG_PG_PORT"
  export PGUSER="$FROG_PG_USER"
  export PGDATABASE="${1:-$FROG_PG_DATABASE}"
  export PGPASSWORD="$FROG_PG_PASSWORD"
  export PGSSLMODE="${PGSSLMODE:-prefer}"
}

frog_pg_psql_db() {
  local db="$1"
  shift
  frog_pg_export_libpq "$db"
  psql --no-psqlrc -v ON_ERROR_STOP=1 -X -d "$db" "$@"
}

frog_pg_schema_dump_args() {
  local s
  for s in "${FROG_PG_SCHEMAS[@]}"; do
    printf -- '--schema=%s\n' "$s"
  done
}

frog_pg_schema_count() {
  local db="$1"
  frog_pg_psql_db "$db" -tAc \
    "SELECT COUNT(*) FROM pg_namespace WHERE nspname IN ('auth','content','ops','player','world');"
}

frog_pg_history_relation() {
  local db="$1"
  frog_pg_psql_db "$db" -tAc \
    "SELECT quote_ident(n.nspname) || '.' || quote_ident(c.relname)
     FROM pg_class c
     JOIN pg_namespace n ON n.oid = c.relnamespace
     WHERE c.relname = '__EFMigrationsHistory'
     ORDER BY CASE n.nspname WHEN 'world' THEN 0 WHEN 'public' THEN 1 ELSE 2 END
     LIMIT 1;"
}

frog_pg_quoted_ident() {
  # Validate a database name is a plain identifier (disposable DBs, never user-supplied SQL).
  local name="$1"
  [[ "$name" =~ ^[A-Za-z_][A-Za-z0-9_]*$ ]] || frog_pg_die "refusing unsafe database name: $name"
  printf '%s' "$name"
}
