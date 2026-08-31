# Phase 8 — TEST_RESULTS

Environment: Windows Server 2022 + Ubuntu (CI), .NET SDK **8.0.424**, PostgreSQL **16**.

| Suite | Result | Notes |
| --- | --- | --- |
| Frog.Tests | **PASS** (local) | 324 |
| PostgreSQL integration | pending CI tip | includes P8-I5 mid-progress reconnect + per-kind replay |
| Phase 8 E2E 23-step | pending CI tip | Talk/Visit/Collect/Kill/Craft + counters + reconnect + replay |
| Phase 8 multi-client ×9 | pending CI tip | `Phase8MultiClientE2ETests` |
| Phase 8 Windows smoke ×3 | pending CI tip | MainForm close-during-save + dispose-once |
| Phase 6/7 regression smokes ×3 | pending CI tip | CI |

## SHAs / CI

| Item | Value |
| --- | --- |
| Implementation tip (pre-CI) | see latest commit on `cursor/phase0-baseline-audit-02c7` |
| Final tip + CI URL | filled only after green CI on exact tip (P8-I6) |

## Commands

```bash
dotnet build Frog.Creator.sln -c Release
dotnet test Frog.Tests/Frog.Tests.csproj -c Release
dotnet test tests/Frog.Persistence.IntegrationTests -c Release
# Windows CI: Phase 8 filter FullyQualifiedName~.Phase8 ×3
```

Skipped tests: none reported in Frog.Tests or PostgreSQL integration suites.
