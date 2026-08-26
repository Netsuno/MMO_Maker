# Phase 7 — TEST_RESULTS (remediation verification)

## Environment (local agent)

| Item | Value |
| --- | --- |
| OS | Linux 6.12.94+ x86_64 |
| .NET SDK | 8.0.424 |
| PostgreSQL | `FROG_POSTGRES_TEST_CONNECTION_STRING` **[SET]** (secrets redacted) |
| Working tree commit before evidence tip push | see final tip after docs commit |
| Date (UTC) | 2026-08-26 |

## Commands and results

### Release restore/build (C# 12 / net8.0)

```bash
dotnet build Frog.Creator.sln -c Release
```

| Field | Value |
| --- | --- |
| Status | **PASS** |
| Duration | ~2–3 s |
| Warnings | 0 |
| Errors | 0 |

### Unit / architecture / protocol / in-memory smoke

```bash
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
```

| Field | Value |
| --- | --- |
| Status | **PASS** |
| Passed | **270** |
| Failed | 0 |
| Skipped | 0 |
| Duration | ~5–6 s |

Includes: architecture boundaries, protocol parsers, Phase7* unit tests, `Phase7InMemorySmokeE2ETests`.

### Real PostgreSQL integration + PG headless E2E

```bash
dotnet test tests/Frog.Persistence.IntegrationTests -c Release --no-build
```

| Field | Value |
| --- | --- |
| Status | **PASS** |
| Passed | **66** |
| Failed | 0 |
| Skipped | 0 |
| Duration | ~25–47 s (wall) |

Includes: auth, player repos, economy atomicity, published content visibility, `Phase7PostgresE2ETests` (17-step + multi-client).

### Windows editor smoke ×3

| Field | Value |
| --- | --- |
| Status | **CI** (NOT RUN on Linux agent) |
| Filter | full `Frog.Editor.WindowsSmokeTests` excluding only when CI runs gameplay filter separately |
| Expected | 35 × 3 on Windows CI job |

### Windows gameplay-client smoke ×3

| Field | Value |
| --- | --- |
| Status | **CI** (NOT RUN on Linux agent — requires STA WinForms) |
| Command | `dotnet test tests/Frog.Editor.WindowsSmokeTests/... --filter FullyQualifiedName~GameplayClientSmoke` ×3 |
| Artifact | `phase-07-gameplay-client-screenshots` |
| Manifest | `docs/progress/phase-07-essential-gameplay/SCREENSHOT_MANIFEST.md` |

### git diff --check

| Field | Value |
| --- | --- |
| Status | **PASS** (clean after Application.csproj LF fix) |

## 17-step E2E matrix (`Phase7PostgresE2ETests.FullGameplayFlow_PostgreSqlHeadless_AllSteps`)

| # | Step | Asserted state |
| ---: | --- | --- |
| 1 | Register + login | Token in LoginResult message |
| 2 | Create/list/select published class | Character id + select success |
| 3 | Map load | MapData / MapAlreadySynced |
| 4 | Obtain item (ground pickup) | InventorySnapshot contains item Guid |
| 5 | Equip | Equipped weapon Guid in snapshot |
| 6 | Reconnect + reselect | Equipment persisted in snapshot |
| 7 | Valid melee | MeleeAttackResult success |
| 8 | Valid spell | SpellCastResult + CombatState |
| 9 | Invalid combat | Fail byte; baselines retained |
| 10 | XP once | ExperienceGain / CombatState.experience |
| 11 | Chat map/global/whisper + rate limit | Decoded ChatMessage; spam drained |
| 12 | Shop buy/sell | Inventory + CombatState.gold |
| 13 | Bank item + gold | BankSnapshot slots + BankGold |
| 14 | Death | CombatState.isDead / hp=0 |
| 15 | Respawn | Full HP/MP CombatState |
| 16 | Server restart + reconnect | Equipment + progression persisted |
| 17 | Clean shutdown | Host/clients disposed |

## Multi-client

| Scenario | Test | Result |
| --- | --- | --- |
| Ground pickup one winner | `GroundPickupRace_TwoClients_ExactlyOneWinner` | PASS |
| Combat XP once | `CombatRace_TwoClients_SameMonster_ExactlyOneExperienceGrant` | PASS |
| Shop idempotent retry | `ShopBuy_IdempotentRetry_DoesNotDuplicateItem` | PASS |
| Whisper isolation | `ChatWhisper_DoesNotLeakToThirdParty` | PASS |
| Reconnect displace | `Reconnect_DisplacesStaleConnection` | PASS |

## Suites NOT RUN on Linux agent (must be green on CI tip)

- Windows editor smoke ×3
- Windows gameplay-client smoke ×3
