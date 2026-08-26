# Phase 7 — TEST_RESULTS (remediation verification)

## Identity

| Item | Value |
| --- | --- |
| Implementation SHA (CI-green) | `862d2e3398b23ea38b50ab14a675d3b0579f8cff` |
| CI (implementation) | https://github.com/Netsuno/MMO_Maker/actions/runs/33021897140 |
| build-and-test | https://github.com/Netsuno/MMO_Maker/actions/runs/33021897140/job/98354032665 |
| postgres-integration | https://github.com/Netsuno/MMO_Maker/actions/runs/33021897140/job/98354032884 |
| Date (UTC) | 2026-08-26 |

## Environment (local agent)

| Item | Value |
| --- | --- |
| OS | Linux 6.12.94+ x86_64 |
| .NET SDK | 8.0.424 |
| PostgreSQL | `FROG_POSTGRES_TEST_CONNECTION_STRING` **[SET]** (secrets redacted) |

## Commands and results

### Release restore/build (C# 12 / net8.0)

```bash
dotnet build Frog.Creator.sln -c Release
```

| Field | Value |
| --- | --- |
| Status | **PASS** |
| Warnings | 0 |
| Errors | 0 |

### Unit / architecture / protocol / in-memory smoke

```bash
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
```

| Field | Local | CI tip `862d2e3` |
| --- | --- | --- |
| Status | **PASS** | **PASS** |
| Passed | **271** | **271** |
| Failed | 0 | 0 |
| Skipped | 0 | 0 |

Includes: architecture boundaries, protocol parsers, Phase7* unit tests, `Phase7InMemorySmokeE2ETests` (incl. immediate-reconnect registry regression).

### Real PostgreSQL integration + PG headless E2E

```bash
dotnet test tests/Frog.Persistence.IntegrationTests -c Release --no-build
```

| Field | Local | CI tip `862d2e3` |
| --- | --- | --- |
| Status | N/A (no local PG) | **PASS** |
| Passed | — | **93** |
| Failed | — | 0 |
| Skipped | — | 0 |

Includes: auth, player repos, economy atomicity, published world (`Phase7PublishedWorldTests`), packaged Release server PG process test (`PackagedServerPostgreSqlProcessTests`), `Phase7PostgresE2ETests` (17-step + multi-client).

### Windows editor smoke ×3 (CI)

| Field | Value |
| --- | --- |
| Status | **PASS** on tip |
| Filter | `FullyQualifiedName!~GameplayClientSmoke` |
| Per pass | **35** Passed / 0 Failed / 0 Skipped |
| Consecutive | **3/3** (each attempt 1/2) |
| Log marker | `=== Windows editor smoke 3/3 consecutive passes OK ===` |

### Windows gameplay-client smoke ×3 (CI)

| Field | Value |
| --- | --- |
| Status | **PASS** on tip |
| Filter | `FullyQualifiedName~GameplayClientSmoke` |
| Per pass | **5** Passed / 0 Failed / 0 Skipped |
| Consecutive | **3/3** (each attempt 1/2) |
| Log marker | `=== Gameplay client smoke 3/3 consecutive passes OK ===` |
| Scenarios | register/login/inventory/reconnect + shop/bank/sell + chat rate limit + spell + drop |
| Screenshots | `01`…`05` under artifact `phase-07-gameplay-client-screenshots` |
| Manifest | `SCREENSHOT_MANIFEST.md` |
| Protocol | shop buy + equip via public client UI (no mid-scenario DI inventory inject) |

### git diff --check

| Field | Value |
| --- | --- |
| Status | **PASS** |

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
| Immediate reconnect + select + map (no delay) | `ReconnectSelectThenMapRequest_StaysConnected` | PASS |

## Packaged server (P7-R2)

| Test | Result |
| --- | --- |
| `ReleasePackagedServer_PostgreSqlEnabled_LoginAndShopBuy_Persists` | PASS — Release publish, PG reflection load, shop buy persists, clean teardown |

## Reconnect zombie (P7 smoke blocker fixed in `4d92800`)

Immediate reconnect displace previously left a `ClientRegistry` zombie (AuthenticatedSession nulled without Unregister). Character-select PositionUpdate fan-out then aborted the **new** TCP. Fixed by unregister-on-displace + hardened send/dispatch; regression covered above.
