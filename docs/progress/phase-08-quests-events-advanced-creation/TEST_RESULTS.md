# Phase 8 — TEST_RESULTS

Environment: Windows Server 2022 + Ubuntu (CI), .NET SDK **8.0.424**, PostgreSQL **16**.

| Suite | Result | Notes |
| --- | --- | --- |
| Frog.Tests | **PASS** 313 | 0 skipped |
| PostgreSQL integration | **PASS** 143 | 0 skipped |
| Phase 8 E2E 23-step | **PASS** | `Phase8PostgresE2ETests` |
| Phase 8 multi-client ×8 | **PASS** | `Phase8MultiClientE2ETests` |
| Phase 8 draft invisibility ×7 kinds | **PASS** | Theory |
| Phase 8 Windows smoke ×3 | **PASS** | client functional + editor lifecycle |
| Phase 6/7 regression smokes ×3 | **PASS** | CI |

## SHAs / CI

| Item | Value |
| --- | --- |
| Final tip | `6419054` |
| CI | https://github.com/Netsuno/MMO_Maker/actions/runs/33291387717 |
| Client screenshots artifact | `phase-08-gameplay-client-screenshots` |
| Editor screenshots artifact | `phase-08-editor-screenshots` |

## Commands

```bash
dotnet build Frog.Creator.sln -c Release
dotnet test Frog.Tests/Frog.Tests.csproj -c Release
dotnet test tests/Frog.Persistence.IntegrationTests -c Release
# Windows CI: Phase 8 filter FullyQualifiedName~.Phase8 ×3
```

Skipped tests: none reported in Frog.Tests or PostgreSQL integration suites.
