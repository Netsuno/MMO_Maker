# Phase 9 — PostgreSQL backup / restore runbook (P9-3)

**Status:** implemented on `cursor/phase9-distribution-admin-hardening`.  
**Schema tip:** 25 EF Up migrations, latest `20260917223000_AuthOperators` under `Frog.Persistence.PostgreSql/Migrations/` (P9-2 `auth.operators`; previous tip `20260917204500_MapEventExecutionRequestIdGlobalUnique`).  
**Product schemas (`FrogDbContext`):** `auth`, `content`, `ops`, `player`, `world`.  
**Engine:** PostgreSQL 16 (Compose `postgres:16-alpine`, CI `postgres:16`).

This runbook is the operator path. The automated proof is `PostgresBackupRestoreTests` (see §Proof). Re-run after any new EF migration (notably P9-1 mute/kick/ban tables, when they land).

## Prerequisites

| Piece | Notes |
| --- | --- |
| PostgreSQL 16 server | Dev: `docker compose up -d postgres`. Tests: any PG 16 with `CREATEDB`. |
| Client tools on PATH | `pg_dump`, `pg_restore`, `psql` (package `postgresql-client` / `postgresql-client-16`). Dump and restore **major versions must match** (16→16). Restoring a v16 dump onto v15 is unsupported. |
| Login role | Can connect to the target database. `--create-database` / `--recreate` need `CREATEDB`. `--no-owner --no-acl` so objects become owned by the restoring role (dev `frog`, CI `frog_test`, or a production role). Cluster roles are **not** dumped (`pg_dumpall --globals` is out of scope). |
| Connection string | Npgsql form used by the server and tests, e.g. `Host=127.0.0.1;Port=5432;Database=frog;Username=frog;Password=frog_dev_only`. Scripts also accept libpq `host=… dbname=…`. |
| Empty-migrate path | `Frog.Server` / editor call `Database.Migrate()` on start. Tests use `IsolatedPostgresFixture` / `FrogDbContext.Database.MigrateAsync()`. There is no MariaDB path (ADR-0002). |

Dev Compose (`docker-compose.yml`): user `frog`, password `frog_dev_only`, database `frog`. These are **not** production secrets.

CI postgres-integration installs `postgresql-client` so `pg_dump` / `pg_restore` exist on the Ubuntu runner. The job still uses service `postgres:16` and `FROG_POSTGRES_TEST_CONNECTION_STRING`.

## What is in a backup

`scripts/postgres-backup.sh` (and `scripts/postgres-backup.ps1`) call `pg_dump --format=custom --compress=9 --no-owner --no-acl` for schemas:

- `auth` — accounts / sessions (Phase 7) and `auth.operators` (P9-2)
- `content` — published catalogs, including Phase 8 definitions
- `ops` — includes `ops.legacy_imports` (usually empty on a new world; dumped anyway because it is in `FrogDbContext`)
- `player` — characters, inventory, bank, economy ledgers, quest/event request ids
- `world` — maps, published snapshots, runtime bindings, **and** `world.__EFMigrationsHistory`

The dump is one file (custom format). It is not a cluster dump: other databases on the same instance are untouched. Extensions such as `plpgsql` are not dumped (avoids “already exists” on restore).

**Do not** dump with MariaDB tools. `scripts/apply-frog-mariadb-schema.ps1` is legacy-only.

## Backup

Linux / CI:

```bash
./scripts/postgres-backup.sh \
  --connection 'Host=127.0.0.1;Port=5432;Database=frog;Username=frog;Password=frog_dev_only' \
  --output artifacts/postgres-backups/frog.dump
```

Windows (PostgreSQL client on PATH):

```powershell
./scripts/postgres-backup.ps1 `
  -Connection 'Host=127.0.0.1;Port=5432;Database=frog;Username=frog;Password=frog_dev_only' `
  -Output artifacts/postgres-backups/frog.dump
```

Flags: `--host/--port/--user/--password/--database` override pieces of `--connection`. `--force` overwrites. `--verbose` is passed to `pg_dump`.

Stop or drain the game server first if you need a quiet snapshot of `player` rows (there is no WAL/PITR in this phase). Copy the `.dump` file **off the box** (object storage, another host). Keep more than one generation; this repo does not invent a retention SLA.

`artifacts/` is gitignored. Do not commit dumps.

## Restore

Target must be an **empty** database (no Frog schemas). Order:

1. `createdb` (or `--create-database`) — **do not** `Database.Migrate()` first.
2. `pg_restore` via `postgres-restore.sh`.
3. Optional `Database.Migrate()` — must apply **0** pending.
4. `PostgresDatabaseHealth` OK (or `postgres-verify.sh` + server start).

```bash
./scripts/postgres-restore.sh \
  --connection 'Host=127.0.0.1;Port=5432;Database=frog_restored;Username=frog;Password=frog_dev_only' \
  --input artifacts/postgres-backups/frog.dump \
  --maintenance-database postgres \
  --create-database
```

`--recreate` terminates sessions, `DROP DATABASE`, then `CREATE DATABASE` (destructive; disposable names only). Restore refuses if `auth/content/ops/player/world` already exist.

Windows: `./scripts/postgres-restore.ps1 -InputPath … -CreateDatabase`.

After restore, start `Frog.Server` with `PostgreSql:Enabled=true` pointing at the restored database. Startup `Database.Migrate()` is a no-op when the dump includes the same history rows as the running assembly (25 Ups on this tip).

## Verify

SQL-only (schemas + history row count):

```bash
./scripts/postgres-verify.sh \
  --connection 'Host=127.0.0.1;Port=5432;Database=frog_restored;Username=frog;Password=frog_dev_only'
```

`PostgresDatabaseHealth` (`Frog.Persistence.PostgreSql/PostgresDatabaseHealth.cs`) is the product check: connect + **zero pending migrations** vs the running EF assembly. It is used by tests; there is no HTTP probe yet (P9-5).

Empty database migrates (no dump involved):

```bash
export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release \
  --filter 'FullyQualifiedName~.PostgresHealthTests.EmptyDatabase_Migrates_AndHealthPasses'
```

That existing test still checks `content`, `ops`, `world`. P9-3 additionally asserts `auth` and `player` on the restored copy.

## Proof (CI-safe)

Documented command (also `scripts/postgres-backup-restore-smoke.sh`):

```bash
export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
./scripts/postgres-backup-restore-smoke.sh
# equivalent:
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release \
  --filter 'FullyQualifiedName~.PostgresBackupRestoreTests'
```

The test (runs in job `postgres-integration` once `postgresql-client` is installed):

1. `CREATE DATABASE` (disposable) → `Database.MigrateAsync()` on empty → `PostgresDatabaseHealth` OK.
2. Seed Phase 7 published world + a PBKDF2 account.
3. Invoke `scripts/postgres-backup.sh` / `postgres-restore.sh` (real `pg_dump` / `pg_restore`).
4. Health OK on the restored DB; pending = 0; `MigrateAsync()` still 0 pending; schemas `auth,content,ops,player,world`.
5. Restored password verifies; Phase 7 TCP login of the restored account succeeds; a new register/login/character-create works on the restored DB.
6. A second restore onto the same target is rejected (non-empty schemas).

Operator roundtrip of a live DB into a disposable copy (does not replace the health/login proof):

```bash
./scripts/postgres-backup-restore-smoke.sh --roundtrip \
  --connection 'Host=127.0.0.1;Port=5432;Database=frog;Username=frog;Password=frog_dev_only'
```

## Failure modes

| Symptom | Likely cause | What to do |
| --- | --- | --- |
| `pg_restore: error: … already exists` / restore says target already has Frog schemas | Migrated the target before restore, or restoring twice | Drop/recreate empty DB (`--recreate` on a disposable name) or pick a new database. Never migrate then restore. |
| `PostgresDatabaseHealth` pending after restore | Dump taken before a new EF migration (e.g. future P9-1 ban tables) | Restore then start the new server so `Database.Migrate()` applies leftover Ups. Take a fresh dump after migrate. Re-prove this runbook. |
| Health pending on a dump that should match | Dump missed `world.__EFMigrationsHistory` (schema filter too narrow) | Use the scripts in this folder, not a hand-rolled `pg_dump` of a subset of tables. |
| `extension "plpgsql" already exists` with `--exit-on-error` | Whole-database dump including extensions | Scripts dump only the five product schemas. |
| Permission denied / not owner | Restoring role cannot create schemas | Use a role with `CREATEDB` + create rights, or restore as the database owner. `--no-owner` avoids replay of the original role name. |
| Version mismatch | pg_dump 16 vs server 15 (or the reverse) | Use PostgreSQL 16 clients against PostgreSQL 16 servers. |
| Login fails after restore | Dump from a different world, or server still pointed at the old DSN | Confirm `PostgreSql:ConnectionString` / `FROG_POSTGRES_CONNECTION_STRING` is the restored database. |
| `CREATE DATABASE` fails | Role lacks `CREATEDB`; maintenance DB wrong | Grant `CREATEDB`, or pre-create the empty database and omit `--create-database`. `--maintenance-database` defaults to `postgres` (always present on the official image). Integration tests use the admin database from `FROG_POSTGRES_TEST_CONNECTION_STRING` as maintenance via `CREATE DATABASE` in C#. |
| Restore onto Compose volume with leftover data | `frog_pg_data` still has schemas | `--recreate` a **different** database name, or `docker compose down -v` (destroys the whole dev volume). |

## Residual risks

- **P9-1 migrations:** mute/kick/ban tables are not in the current 25-migration tip (P9-2 added `auth.operators` only). A dump taken now restores cleanly today; after P9-1, either migrate-after-restore or take a new dump. Re-run `PostgresBackupRestoreTests`.
- **Older dumps:** a backup taken before `20260917223000_AuthOperators` restores, then `Database.Migrate()` applies the operators table. Prefer a fresh dump after each accepted migration.
- **P9-2 secret handling:** Compose passwords in examples are dev-only. Production connection strings stay in gitignored `appsettings.Local.json`.
- No PITR / replication slot / off-box scheduler is provided. Operators still need a copy of the `.dump` file somewhere else.
- `docs/DATA_MODEL.md` remains incomplete vs `FrogDbContext`; this runbook lists the five schemas rather than rewriting that file.
- `PostgresDatabaseHealth` is not an HTTP liveness endpoint.

## Out of scope

- MariaDB dump / `scripts/apply-frog-mariadb-schema.ps1`
- Point-in-time replication
- Guild / mute / kick / ban schema (P9-1 / P9-S)
- Phase 10
