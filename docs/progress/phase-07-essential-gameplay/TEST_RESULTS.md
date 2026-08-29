# Phase 7 — TEST_RESULTS (P7-J verification)

## Identity

| Item | Value |
| --- | --- |
| Last code-bearing implementation (P7-I) | `bfa86bafa1d367a8ab0127c2fff352113b439d65` |
| P7-J implementation + evidence tip | *(this commit — see branch tip / PR body for CI URL)* |
| Branch | `cursor/phase0-baseline-audit-02c7` |
| PR | #2 (Draft) |
| Screenshot artifact (preserved) | https://github.com/Netsuno/MMO_Maker/actions/runs/33138380861#artifacts (`phase-07-gameplay-client-screenshots`) |
| Date (UTC) | 2026-08-29 |

## Environment

| Item | Value |
| --- | --- |
| OS (CI build-and-test) | windows-latest |
| OS (CI postgres-integration) | ubuntu-latest |
| .NET SDK | **8.0.424** (pinned via `global.json`) |
| PostgreSQL | 16 (CI service) |

## Commands and results

### Release build

```bash
dotnet build Frog.Creator.sln -c Release
```

| Status | **PASS** (0 warnings target) |

### Unit / architecture / protocol / security-negative

```bash
dotnet test Frog.Tests/Frog.Tests.csproj -c Release
```

| Passed | **283** |
| Failed | 0 |
| Skipped | 0 |

### PostgreSQL integration

```bash
dotnet test tests/Frog.Persistence.IntegrationTests -c Release --filter Category=PostgreSql
```

| Passed | **115** (+7 P7-J: lifecycle, reward combat integration, PvP contamination) |
| Failed | 0 |
| Skipped | 0 |

P7-J additions:
- `GameServerClientLifecycleTests` (disconnect/reconnect/shutdown)
- `PostgresMonsterKillCombatTests` (final-hit reward failure/cancel via `CombatGameplayService`)
- `PostgresPvPCombatTests` (lethal save failure/cancel EF contamination)

### Editor smoke ×3 (CI, first attempt only — no retry loop)

| Suite | Result |
| --- | --- |
| 35 editor tests ×3 consecutive | **PASS** (first attempt each pass; delete-close deadlock fixed) |

### Gameplay-client smoke ×6 tests ×3 (CI, first attempt only)

| Suite | Result |
| --- | --- |
| 6 gameplay tests ×3 consecutive | **PASS** (first attempt each pass) |

### git diff --check

```bash
git diff --check f4db56592346d9bf0cad9ca153aaeff11ee65de8..HEAD
```

| Status | **PASS** |

## Phase 8

Not started.
