# Phase 7 — TEST_RESULTS (P7-H1…H5 verification)

## Identity

| Item | Value |
| --- | --- |
| Implementation SHA (local green) | `46df9a6e8756af1d21ac124af0df857f65c7a626` |
| Evidence tip | pending after CI |
| Branch tip (PR body) | `46df9a6e8756af1d21ac124af0df857f65c7a626` |
| CI (implementation) | pending |
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

| Passed | **279** |
| Failed | 0 |
| Skipped | 0 |

Includes new: `Phase7EquipPersistenceTests`, `Phase7PvPCombatTests`, `Phase7MonsterKillRewardTests`.

### PostgreSQL integration (zero Phase 7 skips)

```bash
dotnet test tests/Frog.Persistence.IntegrationTests -c Release --filter Category=PostgreSql
```

| Passed | **99** |
| Failed | 0 |
| Skipped | 0 |

Includes: `GameServerGracefulShutdownTests`, `ShopBuyRace_TwoClients_FinalStockUnit_ExactlyOneWinner`, full `Phase7PostgresE2ETests` 17-step flow.

### Multi-client matrix (corrected)

| Scenario | Clients | Evidence |
| --- | --- | --- |
| Shop idempotent retry | 1 TCP | `ShopBuy_IdempotentRetry_DoesNotDuplicateItem` |
| Shop final-stock race | **2 TCP**, 2 characters, distinct request IDs | `ShopBuyRace_TwoClients_FinalStockUnit_ExactlyOneWinner` |
| Ground pickup race | 2 TCP | `GroundPickupRace_TwoClients_ExactlyOneWinner` |
| Monster XP race | 2 TCP | `CombatRace_TwoClients_SameMonster_ExactlyOneExperienceGrant` |

### Windows smokes (CI required)

| Suite | Expected |
| --- | --- |
| Editor smoke ×3 | 35 PASS each |
| Gameplay-client smoke ×6 tests ×3 | includes inventory selection, PvP death/respawn, screenshot flows |

### git diff --check

```bash
git diff --check f4db56592346d9bf0cad9ca153aaeff11ee65de8..HEAD
```

| Status | **PASS** (after manifest whitespace fix) |

## Phase 8
Not started.
