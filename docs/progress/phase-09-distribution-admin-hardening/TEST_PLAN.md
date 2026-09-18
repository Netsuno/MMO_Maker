# Phase 9 — TEST_PLAN

**Status:** P9-6 **DONE**. CI on product tip `66fa070` and evidence pack tip `8bf6f08` is **SUCCESS**. Phase 9 is **READY** (residuals listed in `KNOWN_ISSUES.md`). Do not weaken Phase 8 suites or screenshot SHA gates.

## Identity (do not invent)

| Item | Value |
| --- | --- |
| Branch | `cursor/phase9-distribution-admin-hardening` |
| Product tip | `66fa070b07352c5ee0429234bb31470e10608805` (`66fa070`) / https://github.com/Netsuno/MMO_Maker/actions/runs/35291880532 **SUCCESS** |
| Evidence pack tip | `8bf6f088cc002ba8957d062952843a82706600a1` (`8bf6f08`) / https://github.com/Netsuno/MMO_Maker/actions/runs/35292542956 **SUCCESS** |
| Draft PR | https://github.com/Netsuno/MMO_Maker/pull/7 |
| `build-and-test` (`8bf6f08`) | **success** 9m41s — https://github.com/Netsuno/MMO_Maker/actions/runs/35292542956/job/105438279174 |
| `postgres-integration` (`8bf6f08`) | **success** 4m54s — https://github.com/Netsuno/MMO_Maker/actions/runs/35292542956/job/105438279075 |
| Earlier completed CI on this PR | https://github.com/Netsuno/MMO_Maker/actions/runs/35288813093 SUCCESS on P9-0 docs-only tip `6f178e9`. Later product commits before `66fa070` were **cancelled** (`cancel-in-progress`) — not green evidence. |
| Baseline CI (main, branch-start SHA `5af47b9`) | https://github.com/Netsuno/MMO_Maker/actions/runs/35286923042 SUCCESS |
| Protocol | `FrogWireProtocol.Version = 10` |

## Counts from CI logs

Same totals on product tip run 35291880532 and evidence tip run 35292542956 (`Test Run Successful` summaries). No Failed/Skipped lines were printed next to these totals. Quoted from `gh run view … --log`.

| Step (job) | Log line |
| --- | --- |
| Test unitaires (Release) | `Total tests: 436` / `Passed: 436` |
| Smoke UI éditeur Windows ×3 | `Total tests: 87` / `Passed: 87` on each of three consecutive first-attempt passes; log: `Windows editor smoke 3/3 consecutive first-attempt passes OK` |
| Gameplay client smoke ×3 | `Total tests: 6` / `Passed: 6` ×3; log: `Gameplay client smoke 3/3 consecutive first-attempt passes OK` |
| Phase 8 smoke ×3 | `Total tests: 24` / `Passed: 24` ×3; log: `Phase 8 smoke 3/3 consecutive passes OK (manifest gate verified)` |
| Phase 8 screenshot SHA | `Phase 8 screenshot manifest verification OK (12 file(s): 12 exact-sha, 0 present-dims; distinct-frame checks passed).` |
| Integration tests (PostgreSQL) | `Total tests: 180` / `Passed: 180` |
| Publish layout smoke | `./scripts/packaged-server-smoke.sh --layout-only` → `layout-only smoke OK` |

## Baseline suites (do not weaken)

| Suite | Project | Phase 8 acceptance (main) | This branch CI (`66fa070` / 35291880532) |
| --- | --- | --- | --- |
| Unit / in-memory | `Frog.Tests/Frog.Tests.csproj` | **412** PASS | **436** Passed / 436 total |
| PostgreSQL integration | `tests/Frog.Persistence.IntegrationTests/` | **174** PASS | **180** Passed / 180 total |
| Editor smoke | `tests/Frog.Editor.WindowsSmokeTests/` filter `!~GameplayClientSmoke&!~Phase8` | **87×3** | **87×3** |
| Gameplay smoke | same, `GameplayClientSmokeTests` | **6×3** | **6×3** |
| Phase 8 smoke | same, `FullyQualifiedName~.Phase8` | **24×3** | **24×3** + 12-file exact-sha manifest OK |

Phase 8 screenshot SHA-256 table is unchanged on this branch (`SCREENSHOT_MANIFEST.md`; capture tip `fa8c44f`). `git diff origin/main...HEAD` for `scripts/verify-phase8-screenshot-manifest.ps1` (and the test/update siblings) is **empty**.

## Phase 9 tests (inventory)

| Task | Coverage | Filter / command |
| --- | --- | --- |
| P9-1 | Mute / kick / ban unit + in-memory TCP (`Frog.Tests/Phase9ModerationTests.cs`, 5 `[Fact]`). PG persist + host restart + TCP (`tests/Frog.Persistence.IntegrationTests/Phase9ModerationTests.cs`, 2 `[PostgresFact]`). Phase 7 Global/Map/Whisper regression is inside the in-memory TCP Fact. | `FullyQualifiedName~.Phase9ModerationTests` |
| P9-2 | Unprivileged / secrets / bind / WorldFlags (`Frog.Tests/Phase9SecurityGateTests.cs`, 11 `[Fact]`). PG operator + WorldFlags TCP (`tests/Frog.Persistence.IntegrationTests/Phase9SecurityGateTests.cs`, 2 `[PostgresFact]`). Extra: `Phase7InMemorySmokeE2ETests.WorldFlagsPatchRequest_RejectedInProductionComposition` (+1 `[Fact]`). | `FullyQualifiedName~.Phase9SecurityGateTests` |
| P9-3 | Empty migrate → seed → `pg_dump` → `pg_restore` → `PostgresDatabaseHealth` OK → Phase 7 TCP login (`PostgresBackupRestoreTests`, 1 `[PostgresFact]`). Included in the CI **180**. | `FullyQualifiedName~.PostgresBackupRestoreTests` · `./scripts/postgres-backup-restore-smoke.sh` |
| P9-4 | Script/guide presence + Local overlay never published + PG sidecar RID (`Frog.Tests/Phase9PackagingTests.cs`, 3 `[Fact]`). Packaged process + PG login/shop (`PackagedServerPostgreSqlProcessTests`). Layout: `./scripts/packaged-server-smoke.sh --layout-only` (**OK** on this run). | |
| P9-5 | In-memory mixed ×4 + counters (`Frog.Tests/Phase9OpsMetricsTests.cs`, 4 `[Fact]`). PG attach ×4 (`PostgresLoadObservabilityTests`, 1 `[PostgresFact]`). Harness: `./scripts/run-load-harness.sh --scenario mixed --sessions 25` | `FullyQualifiedName~.Phase9OpsMetricsTests` |
| P9-S | None | **DEFERRED** |

## Commands

See [`docs/TESTING.md`](../../TESTING.md) and [`BASELINE_AUDIT.md`](BASELINE_AUDIT.md) §6.

```bash
dotnet restore Frog.Creator.sln
dotnet build Frog.Creator.sln -c Release --no-restore
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build

export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release
./scripts/postgres-backup-restore-smoke.sh
```

Windows smokes stay in `.github/workflows/ci.yml` (`build-and-test` on `windows-latest`).
