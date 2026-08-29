# Phase 7 — TEST_RESULTS (P7-K verification)

## Identity

| Item | Value |
| --- | --- |
| P7-I code-bearing tip | `bfa86bafa1d367a8ab0127c2fff352113b439d65` |
| P7-J implementation tip | `947e665cf53ebad2d176868415f9f95a586c0e6a` |
| P7-K implementation tip | `2f107b3cdb9a677a00992b2296262c78eaff7c6a` |
| Branch | `cursor/phase0-baseline-audit-02c7` |
| PR | #2 (Draft) |
| CI (final green) | https://github.com/Netsuno/MMO_Maker/actions/runs/33252829298 |
| Screenshot artifact (preserved) | https://github.com/Netsuno/MMO_Maker/actions/runs/33138380861#artifacts |
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

| Status | **PASS** (0 warnings, 0 errors; SDK 8.0.424) |

### Unit / architecture / protocol / security-negative

```bash
dotnet test Frog.Tests/Frog.Tests.csproj -c Release
```

| Passed | **284** |
| Failed | 0 |
| Skipped | 0 |
| Duration | ~18 s (local) / ~18 s (CI) |

### PostgreSQL integration

```bash
dotnet test tests/Frog.Persistence.IntegrationTests -c Release --filter Category=PostgreSql
```

| Passed | **115** |
| Failed | 0 |
| Skipped | 0 |
| Duration | ~2m17s (CI job) |
| Raw log guard | **PASS** (no handler faults, NRE, ODE, BackgroundService failed) |

### Editor smoke ×3 (CI, first attempt only)

| Suite | Result |
| --- | --- |
| 35 editor tests ×3 consecutive | **PASS** (first attempt each pass) |
| Raw log guard | **PASS** (no Unhandled exception, Win32 1406, locked-assembly warnings) |

### Gameplay-client smoke ×6 tests ×3 (CI, first attempt only)

| Suite | Result |
| --- | --- |
| 6 gameplay tests ×3 consecutive | **PASS** (first attempt each pass; StopAsync observed) |
| Raw log guard | **PASS** |

### git diff --check

```bash
git diff --check f4db56592346d9bf0cad9ca153aaeff11ee65de8..HEAD
```

| Status | **PASS** |

### git status

| Status | **clean** (untracked `artifacts/` local only) |

## P7-K lifecycle verification

| Blocker | Fix | Verified |
| --- | --- | --- |
| P7-K1 reconnect displacement handler NRE | Immutable `RemoteEndPoint`; `Phase7TestLogCollector` in lifecycle tests | PG CI log guard PASS |
| P7-K2 WinForms unhandled + unobserved StopAsync | STA exception hooks + suite shutdown; `StopAsync` observed | Windows CI log guard PASS ×6 |

## Phase 8

Not started.
