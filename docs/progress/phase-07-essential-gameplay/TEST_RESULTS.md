# Phase 7 — TEST_RESULTS (P7-I1…I4 verification)

## Identity

| Item | Value |
| --- | --- |
| Implementation SHA (local green) | `fefa07080583c4cc53f16fd52f1606bac90b1442` |
| Evidence tip | `fefa07080583c4cc53f16fd52f1606bac90b1442` (pending CI confirmation) |
| Branch tip (PR body) | `fefa07080583c4cc53f16fd52f1606bac90b1442` |
| CI (implementation) | pending — https://github.com/Netsuno/MMO_Maker/actions |
| Date (UTC) | 2026-08-28 |

## Environment (local agent)

| Item | Value |
| --- | --- |
| OS | Linux 6.12.94+ x86_64 |
| .NET SDK | 8.0.424 |
| PostgreSQL | `FROG_POSTGRES_TEST_CONNECTION_STRING` **[SET]** |

## Commands and results (local)

### Release build

```bash
dotnet build Frog.Creator.sln -c Release
```

| Status | **PASS** |

### Unit / architecture / protocol / security-negative

```bash
dotnet test Frog.Tests/Frog.Tests.csproj -c Release
```

| Passed | **283** |
| Failed | 0 |
| Skipped | 0 |

Includes: `Phase7PvPCombatTests` (concurrent + lethal save failure/cancel), `Phase7MonsterKillRewardTests` (restore/retry/cancel through combat service).

### PostgreSQL integration (zero Phase 7 skips)

```bash
dotnet test tests/Frog.Persistence.IntegrationTests -c Release --filter Category=PostgreSql
```

| Passed | **108** |
| Failed | 0 |
| Skipped | 0 |

Includes new: `PostgresMonsterKillRewardTests` (grant, replay, race, fail/cancel/retry), `PostgresPvPCombatTests` (concurrent death, lethal save failure/cancel).

### Windows smokes (CI required — not runnable on Linux agent)

| Suite | Local | CI |
| --- | --- | --- |
| Editor smoke ×3 (35 tests each) | N/A (no WindowsDesktop) | pending |
| Gameplay-client smoke ×6 tests ×3 | N/A (no WindowsDesktop) | pending |

### git diff --check

```bash
git diff --check f4db56592346d9bf0cad9ca153aaeff11ee65de8..HEAD
```

| Status | **PASS** (Frog.Application.csproj normalized to LF) |

### git status

| Status | **clean** (after commit `fefa070`) |

## Phase 8

Not started.
