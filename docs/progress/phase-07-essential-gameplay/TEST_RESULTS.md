# Phase 7 — TEST_RESULTS (remediation verification)

## Identity

| Item | Value |
| --- | --- |
| Implementation SHA (CI-green) | `4d92800b338fe71aef8ba9f2c8b1dcc8e2a72976` |
| Evidence tip | `ca6f85e1f97ad97cf4bd313045e3a8596c3b8a76` |
| CI (implementation) | https://github.com/Netsuno/MMO_Maker/actions/runs/32970817258 |
| CI (evidence tip) | https://github.com/Netsuno/MMO_Maker/actions/runs/32971367882 |
| build-and-test (impl) | https://github.com/Netsuno/MMO_Maker/actions/runs/32970817258/job/98183871807 |
| postgres-integration (impl) | https://github.com/Netsuno/MMO_Maker/actions/runs/32970817258/job/98183871544 |
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

| Field | Local | CI tip `4d92800` |
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

| Field | Local | CI tip `4d92800` |
| --- | --- | --- |
| Status | **PASS** | **PASS** |
| Passed | **66** | **66** |
| Failed | 0 | 0 |
| Skipped | 0 | 0 |

Includes: auth, player repos, economy atomicity, published content visibility, `Phase7PostgresE2ETests` (17-step + multi-client).

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
| Per pass | **1** Passed / 0 Failed / 0 Skipped |
| Consecutive | **3/3** (each attempt 1/2) |
| Log marker | `=== Gameplay client smoke 3/3 consecutive passes OK ===` |
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

## Reconnect zombie (P7 smoke blocker fixed in `4d92800`)

Immediate reconnect displace previously left a `ClientRegistry` zombie (AuthenticatedSession nulled without Unregister). Character-select PositionUpdate fan-out then aborted the **new** TCP. Fixed by unregister-on-displace + hardened send/dispatch; regression covered above.
