# Phase 9 — TEST_PLAN

**Status:** P9-2 tests and P9-3 backup/restore integration test added. P9-1 / P9-4…P9-6 still TBD. Do not weaken Phase 8 suites.

## Baseline suites (already on main — do not weaken)

| Suite | Project | Phase 8 acceptance count | Notes |
| --- | --- | --- | --- |
| Unit / in-memory | `Frog.Tests/Frog.Tests.csproj` | 412 PASS, 0 skipped | Includes protocol, auth, movement rate, Phase 8 planner |
| PostgreSQL integration | `tests/Frog.Persistence.IntegrationTests/` | 174 PASS, 0 skipped | Needs `FROG_POSTGRES_TEST_CONNECTION_STRING` |
| Editor smoke | `tests/Frog.Editor.WindowsSmokeTests/` filter `!~GameplayClientSmoke&!~Phase8` | 87×3 | Windows CI |
| Gameplay smoke | same, `GameplayClientSmokeTests` | 6×3 | Windows CI |
| Phase 8 smoke | same, `FullyQualifiedName~.Phase8` | 24×3 | Screenshot SHA gate must stay |

Baseline main CI: https://github.com/Netsuno/MMO_Maker/actions/runs/35286923042 — **this branch tip has no CI URL yet**.

## Tests required later

| Task | Planned coverage | Status |
| --- | --- | --- |
| P9-1 | Mute / kick / ban unit + PG persist + TCP enforcement | TBD |
| P9-2 | Unprivileged deny (`IOperatorDirectory`); WorldFlagsPatch rejected in PG prod + production composition; committed secrets are placeholders; Local.json gitignored | **DONE** (unit + PG) |
| P9-3 | Migrate empty → seed → `pg_dump` → `pg_restore` → `PostgresDatabaseHealth` OK → Phase 7 TCP login | **DONE** — `PostgresBackupRestoreTests`; see `BACKUP_RESTORE_RUNBOOK.md` |
| P9-4 | Published layout starts; client/editor still smoke on Windows | TBD |
| P9-5 | Load harness against proposed thresholds in BASELINE_AUDIT | TBD |
| P9-S | None | DEFERRED |

## Commands (unchanged)

See [`docs/TESTING.md`](../../TESTING.md) and [`BASELINE_AUDIT.md`](BASELINE_AUDIT.md) §6.

P9-3 backup/restore smoke (needs `FROG_POSTGRES_TEST_CONNECTION_STRING` and `pg_dump` / `pg_restore`):

```bash
./scripts/postgres-backup-restore-smoke.sh
```
