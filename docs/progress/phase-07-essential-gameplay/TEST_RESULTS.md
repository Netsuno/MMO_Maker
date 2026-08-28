# Phase 7 — TEST_RESULTS (P7-I1…I4 verification)

## Identity

| Item | Value |
| --- | --- |
| Implementation SHA | `bfa86bafa1d367a8ab0127c2fff352113b439d65` |
| Evidence tip | `bfa86bafa1d367a8ab0127c2fff352113b439d65` |
| Branch tip (PR body) | `bfa86bafa1d367a8ab0127c2fff352113b439d65` |
| CI (final green) | https://github.com/Netsuno/MMO_Maker/actions/runs/33138380861 |
| Screenshot artifact | https://github.com/Netsuno/MMO_Maker/actions/runs/33138380861#artifacts (`phase-07-gameplay-client-screenshots`) |
| Date (UTC) | 2026-08-28 |

## Environment

| Item | Value |
| --- | --- |
| OS (local agent) | Linux 6.12.94+ x86_64 |
| OS (CI gameplay smokes) | windows-latest |
| .NET SDK | 8.0.424 |
| PostgreSQL | `FROG_POSTGRES_TEST_CONNECTION_STRING` **[SET]** |

## Commands and results

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

### PostgreSQL integration (zero Phase 7 skips)

```bash
dotnet test tests/Frog.Persistence.IntegrationTests -c Release --filter Category=PostgreSql
```

| Passed | **108** |
| Failed | 0 |
| Skipped | 0 |

### Editor smoke ×3 (CI)

| Suite | Result |
| --- | --- |
| 35 editor tests ×3 consecutive | **PASS** (first attempt each pass) |

### Gameplay-client smoke ×6 tests ×3 (CI)

| Suite | Result |
| --- | --- |
| 6 gameplay tests ×3 consecutive | **PASS** (first attempt each pass; ~36 s per pass) |

### git diff --check

```bash
git diff --check f4db56592346d9bf0cad9ca153aaeff11ee65de8..HEAD
```

| Status | **PASS** |

### git status

| Status | **clean** (after evidence commit) |

## Phase 8

Not started.
