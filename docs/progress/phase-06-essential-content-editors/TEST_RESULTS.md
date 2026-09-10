# Phase 6 — Test Results (Final targeted fix pass)

## Implementation SHA

`99b782f8f205c0161c0bba8838d041714e39947e`

## CI (implementation)

https://github.com/Netsuno/MMO_Maker/actions/runs/32797918806 — **success**

## Commands preserved

```bash
dotnet build Frog.Creator.sln -c Release
dotnet test Frog.Tests/Frog.Tests.csproj -c Release
FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only' \
  dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release
# Windows smoke ×3 (CI job build-and-test):
FROG_EDITOR_FORCE_IN_MEMORY=1 dotnet test tests/Frog.Editor.WindowsSmokeTests/Frog.Editor.WindowsSmokeTests.csproj -c Release --no-build
```

## Verified counts

| Suite | Count | Result |
| --- | ---: | --- |
| Frog.Tests (unit) | 244 | PASS |
| PostgreSQL integration | 38 | PASS |
| Windows smoke | 35 × 3 consecutive | PASS |

## Close-lifecycle smokes (P6-D1)

| Case | Result |
| --- | --- |
| Serialized ops on owning STA UI thread | PASS |
| Real `form.Close()` during refresh/save/publish/delete | PASS |
| Real `form.Close()` during initialization | PASS |
| Non-cooperative timeout keeps form/scope; retry closes | PASS |

## Screenshot evidence (P6-D2)

Committed PNGs are the exact CI artifact outputs from run `32797918806`.

- Artifact: https://github.com/Netsuno/MMO_Maker/actions/runs/32797918806#artifacts (`phase-06-game-data-screenshots`)
- Manifest: `SCREENSHOT_MANIFEST.md` (SHA-256 matches committed files and artifact)

## Phase 7

Not started.
