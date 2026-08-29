# Phase 8 — TEST_RESULTS

Environment: Ubuntu 24.04 / Windows Server 2022 (CI), .NET SDK **8.0.424**, PostgreSQL **16** (CI service `postgres:16`).

| Suite | Command | Result | Passed | Failed | Skipped | Duration |
| --- | --- | --- | ---: | ---: | ---: | --- |
| Frog.Tests (unit/arch/protocol/security) | `dotnet test Frog.Tests/Frog.Tests.csproj -c Release` | **PASS** | 297 | 0 | 0 | ~17s |
| PostgreSQL integration | `dotnet test tests/Frog.Persistence.IntegrationTests -c Release` | **PASS** (CI) | 137 | 0 | 0 | CI |
| Phase 8 E2E (23-step matrix) | `Phase8PostgresE2ETests.FullScenario_AllMatrixSteps` | **PASS** (CI) | 1 | 0 | 0 | CI |
| Phase 8 multi-client | `Phase8MultiClientE2ETests` (8 tests) | **PASS** (CI) | 8 | 0 | 0 | CI |
| Phase 8 PG repos | `PostgresQuestMutationRepositoryTests`, `PostgresEventCraftRepositoryTests`, `PostgresPhase8ContentRepositoryTests` | **PASS** (CI) | 9 | 0 | 0 | CI |
| Windows editor smoke (Phase 6) | CI ×3 consecutive | **PASS** (CI) | — | 0 | 0 | CI |
| Phase 7 gameplay client smoke | CI ×3 consecutive | **PASS** (CI) | — | 0 | 0 | CI |
| **Phase 8 gameplay client smoke** | CI filter `Phase8GameplayClientSmoke` ×3 | **PASS** (CI) | 3 | 0 | 0 | CI |

## Phase 8 PostgreSQL additions (18 tests)

- `PostgresPhase8ContentRepositoryTests` — draft invisibility + publish
- `PostgresQuestMutationRepositoryTests` — idempotency, concurrency, rollback, restart
- `PostgresEventCraftRepositoryTests` — idempotency, concurrency, rollback, restart
- `Phase8PostgresE2ETests` — full 23-step headless network matrix
- `Phase8MultiClientE2ETests` — 8 concurrency/isolation scenarios

## Evidence SHAs

| Item | SHA |
| --- | --- |
| Phase 8 implementation (remediation merge) | `696a2afb079baebc05cda93a70579f617a27c50f` (pre-final smoke/doc tip) |
| Final gate tip | `802a78f` |
| CI (green) | https://github.com/Netsuno/MMO_Maker/actions/runs/33269436387 |

## Logs / artifacts

- CI PostgreSQL log: `artifacts/ci-logs/postgres-integration.log`
- Phase 8 Windows screenshots: `artifacts/phase-08-gameplay-client/` (uploaded as `phase-08-gameplay-client-screenshots`)
- Screenshot manifest: `SCREENSHOT_MANIFEST.md`

## Scans (gate)

- `NotImplementedException` / placeholder scans: no new Phase 8 production paths
- Skipped tests: 0 in Frog.Tests and Frog.Persistence.IntegrationTests Phase 8 suites
- `git diff --check`: Frog.Persistence.PostgreSql.csproj and README clean; pre-existing Frog.Legacy.csproj CRLF vs main unchanged
