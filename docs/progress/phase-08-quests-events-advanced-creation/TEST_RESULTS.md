# Phase 8 — TEST_RESULTS

## Identity

| Item | Value |
| --- | --- |
| Phase 8 starting tip | `3be393b756f32337972432a0571ffabd06a306bb` |
| Starting-tip CI | https://github.com/Netsuno/MMO_Maker/actions/runs/33254661855 |
| Branch | `cursor/phase0-baseline-audit-02c7` |
| PR | #2 (Draft) |
| Date (UTC) | 2026-08-29 |

## Phase 7 regression (unchanged at Phase 8 start)

| Command | Status | Passed | Failed | Skipped |
| --- | --- | ---: | ---: | ---: |
| `dotnet build Frog.Creator.sln -c Release` | PASS | — | 0 | — |
| `dotnet test Frog.Tests/Frog.Tests.csproj -c Release` | PASS | 284 | 0 | 0 |
| PG integration (CI) | PASS | 115 | 0 | 0 |
| Editor smoke ×3 | PASS | 35×3 | 0 | 0 |
| Gameplay smoke ×3 | PASS | 6×3 | 0 | 0 |
| Lifecycle log guards | PASS | 7/7 | — | — |

## Phase 8 functional tests

_Not yet executed — updated per tranche._

## Phase 9

Not started.
