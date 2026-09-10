# Phase 8 — TEST_RESULTS

Environment: Windows Server 2022 + Ubuntu (CI), .NET SDK **8.0.424**, PostgreSQL **16**.

| Suite | Result | Notes |
| --- | --- | --- |
| Frog.Tests | **PASS** | **379** passed, 0 skipped — CI 34436843321 |
| PostgreSQL integration | **PASS** | **159** passed, 0 skipped — includes P8-I5 mid-progress reconnect + per-kind replay |
| Phase 8 E2E 23-step | **PASS** | Talk/Visit/Collect/Kill/Craft + counters + reconnect + replay |
| Phase 8 multi-client ×9 | **PASS** | `Phase8MultiClientE2ETests` |
| Phase8 smoke ×3 | **PASS** | **24×3** |
| Editor smoke ×3 | **PASS** | **56×3** |
| Gameplay smoke ×3 | **PASS** | **6×3** |
| Phase 6/7 regression smokes ×3 | **PASS** | included in Editor / Gameplay Windows CI repeats |

## SHAs / CI

| Item | Value |
| --- | --- |
| Implementation tip | `ebc96921d8e40f1ddf2779dddd50cecd39bb4d45` |
| Manifest tip | `9ddbd5ee0d015e0d60afce5d9f4c3d214b970ecb` |
| Narrative tip | this commit on `cursor/phase0-baseline-audit-02c7` (PR #2) |
| CI (green) | https://github.com/Netsuno/MMO_Maker/actions/runs/34436843321 |

## Commands

```bash
dotnet build Frog.Creator.sln -c Release
dotnet test Frog.Tests/Frog.Tests.csproj -c Release
dotnet test tests/Frog.Persistence.IntegrationTests -c Release
# Windows CI: Phase 8 filter FullyQualifiedName~.Phase8 ×3
```

Skipped tests: none reported in Frog.Tests or PostgreSQL integration suites.
