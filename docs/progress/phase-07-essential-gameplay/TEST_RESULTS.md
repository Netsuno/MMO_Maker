# Phase 7 — TEST_RESULTS (P7-G1…G6 remediation verification)

## Identity

| Item | Value |
| --- | --- |
| Implementation SHA (CI-green) | `c1803132522d8dfb31e3a1284755341eb2d243b2` |
| CI (implementation) | https://github.com/Netsuno/MMO_Maker/actions/runs/33036286537 |
| build-and-test | https://github.com/Netsuno/MMO_Maker/actions/runs/33036286537/job/98399567822 |
| postgres-integration | https://github.com/Netsuno/MMO_Maker/actions/runs/33036286537/job/98399567965 |
| Date (UTC) | 2026-08-27 |

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

| Field | Local | CI tip `c180313` |
| --- | --- | --- |
| Status | **PASS** | **PASS** |
| Passed | **272** | **272** |
| Failed | 0 | 0 |
| Skipped | 0 | 0 |

Includes: architecture boundaries, protocol parsers, Phase7* unit tests, `Phase7InMemorySmokeE2ETests` (incl. `CharacterStatsUpdateRequest_RejectedInProductionComposition`), reconnect registry regression.

### Real PostgreSQL integration + PG headless E2E

```bash
dotnet test tests/Frog.Persistence.IntegrationTests -c Release --no-build
```

| Field | Local | CI tip `c180313` |
| --- | --- | --- |
| Status | **PASS** | **PASS** |
| Passed | **97** | **97** |
| Failed | 0 | 0 |
| Skipped | 0 | 0 |
| CI duration | — | ~1.15 min |

Includes: auth, player repos, economy atomicity + request-id uniqueness, inventory-transfer cancel contamination, published world, packaged Release server **graceful** shutdown (`PackagedServerPostgreSqlProcessTests`), `Phase7PostgresE2ETests` (17-step decoded asserts + multi-client).

### Windows editor smoke ×3 (CI)

| Field | Value |
| --- | --- |
| Status | **PASS** on tip |
| Filter | `FullyQualifiedName!~GameplayClientSmoke` |
| Per pass | **35** Passed / 0 Failed / 0 Skipped |
| Consecutive | **3/3** |
| Log marker | `=== Windows editor smoke 3/3 consecutive passes OK ===` |

### Windows gameplay-client smoke ×3 (CI)

| Field | Value |
| --- | --- |
| Status | **PASS** on tip |
| Filter | `FullyQualifiedName~GameplayClientSmoke` |
| Per pass | **5** Passed / 0 Failed / 0 Skipped (~28–29 s each) |
| Consecutive | **3/3** |
| Log marker | `=== Gameplay client smoke 3/3 consecutive passes OK ===` |
| Scenarios | register/login/equip/unequip/reconnect (token-safe logs); shop/bank item+gold/sell success; chat rate limit; melee/spell/invalid/death/respawn; drop+pickup |
| Screenshots | `01`…`05` under artifact `phase-07-gameplay-client-screenshots` |
| Manifest | `SCREENSHOT_MANIFEST.md` (hashes match downloaded artifact) |

### Secret-leak scan

| Check | Result |
| --- | --- |
| Smoke assert token absent from UI log after login/reconnect | PASS (CI) |
| Screenshots `01` / `04` show `Login OK` / `Reconnect OK` only | PASS (visual + log text) |
| `CharacterStatsUpdateRequest` rejected in production composition | PASS (`Frog.Tests`) |

### git diff --check

| Field | Value |
| --- | --- |
| Status | **PASS** |

## 17-step E2E assertions actually performed

`Phase7PostgresE2ETests.FullGameplayFlow_PostgreSqlHeadless_AllSteps` now asserts:

| # | Step | Assertion |
| ---: | --- | --- |
| 1 | Register + login | Success + token decoded (not logged by client under test) |
| 2 | Character list | `CharacterListRequest` → list contains created character |
| 3 | Map | MapData/MapAlreadySynced map id == published runtime map id |
| 4 | Pickup | Inventory contains weapon |
| 5 | Equip | Equipped weapon Guid |
| 6 | Reconnect | Equipment persisted |
| 7 | Melee | MeleeAttackResult success |
| 8 | Valid spell | SpellCastResult success + MP delta |
| 9 | Invalid spell | Pre/post CombatState identical (level/xp/hp/mp/gold) |
| 10 | XP | Exact `CombatFormulas.MonsterExperienceReward` + exactly one ExperienceGain |
| 11 | Chat | Map/global/whisper + Error frame `"Trop de messages"` |
| 12 | Shop buy/sell | Inventory + exact gold deltas |
| 13 | Bank item + gold | Deposit/withdraw both sides (inventory + bank snapshots) |
| 14 | Death | isDead / hp=0 |
| 15 | Respawn | Full HP/MP + map/pixel vs published spawn tiles |
| 16 | Restart | Exact equipment, inventory qty, gold, bank gold, level/XP, position |
| 17 | Shutdown | Host disposed |

## Packaged server (P7-G6)

| Assertion | Result |
| --- | --- |
| Graceful stop via SIGTERM / `FROG_SHUTDOWN_FILE` | PASS |
| Process exit code 0 | PASS |
| PG sessions drain to 0 without `pg_terminate_backend` | PASS |
| Idle client observes orderly close | PASS |
| Force `Kill` only on timeout failure path | PASS |

## Multi-client

| Scenario | Result |
| --- | --- |
| Ground pickup one winner | PASS |
| Combat XP once (exact amount + monster gone + persisted XP) | PASS |
| Shop idempotent retry | PASS |
| Whisper isolation | PASS |
| Reconnect displace | PASS |
| Immediate reconnect + select + map | PASS |
