# Phase 8 — TEST_RESULTS

Environment: Windows Server 2022 + Ubuntu (CI), .NET SDK **8.0.424**, PostgreSQL **16**.

| Suite | Result | Notes |
| --- | --- | --- |
| Frog.Tests | **PASS** | **412** passed, 0 skipped — CI 35274081277 |
| PostgreSQL integration | **PASS** | **174** passed, 0 skipped — includes P8-I5 mid-progress reconnect + per-kind replay + `Phase8InteractIdentityTcpTests` ×6 |
| Phase 8 E2E 23-step | **PASS** | Talk/Visit/Collect/Kill/Craft + counters + reconnect + replay |
| Phase 8 multi-client ×9 | **PASS** | `Phase8MultiClientE2ETests` |
| Phase8 smoke ×3 | **PASS** | **24×3** |
| Editor smoke ×3 | **PASS** | **85×3** (was 56×3) |
| Gameplay smoke ×3 | **PASS** | **6×3** |
| Phase 6/7 regression smokes ×3 | **PASS** | included in Editor / Gameplay Windows CI repeats |

## SHAs / CI

| Item | Value |
| --- | --- |
| Implementation tip | `09e68dfcb86d0b479515d70b13f1bf607afa7926` (P1 Interact identity; R2 remediations + P1 vs prior `ebc96921`) |
| Capture/manifest tip | `fa8c44f937e729e6004481c651a7bf957585d50e` (SCREENSHOT_MANIFEST; keep separate) |
| Prior P8-G evidence pin | `ebc96921d8e40f1ddf2779dddd50cecd39bb4d45` / CI 34436843321 (379 / 159 / Editor 56×3) |
| CI (green) | https://github.com/Netsuno/MMO_Maker/actions/runs/35274081277 |
| Protocol | `FrogWireProtocol.Version = 10` (`InteractRequest` Guid `activationId`) |

## Commands

```bash
dotnet build Frog.Creator.sln -c Release
dotnet test Frog.Tests/Frog.Tests.csproj -c Release
dotnet test tests/Frog.Persistence.IntegrationTests -c Release
# Windows CI: Phase 8 filter FullyQualifiedName~.Phase8 ×3
```

Skipped tests: none reported in Frog.Tests or PostgreSQL integration suites.
