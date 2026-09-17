# Phase 9 — OPERATIONS_RUNBOOK

**Status:** stub (P9-0). Not an operational procedure yet.

## What exists today

- Start/stop: [`BASELINE_AUDIT.md`](BASELINE_AUDIT.md) §6
- Config: `Frog.Server/appsettings.json` + gitignored `appsettings.Local.json`
- Dev PostgreSQL: `docker compose up -d postgres`
- Health primitive: `Frog.Persistence.PostgreSql/PostgresDatabaseHealth.cs` (used in tests; no HTTP probe)

## TBD (P9-4 / P9-2)

- Production bind address and firewall
- How to enable PostgreSQL without committing secrets
- How to apply migrations (today: automatic `Database.Migrate()` on server/editor start)
- How to drain players before stop
- Where logs go besides console
- How an operator mute/kick/ban after P9-1
- What to do when `PostgresDatabaseHealth` reports pending migrations

## Do not

- Enable `MariaDb:enabled` for a new world
- Point production at Compose passwords (`frog_dev_only` / `changeme`)
