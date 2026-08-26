# Phase 7 — Review Request

## Ready for review

`PHASE 7 GATE REACHED — WAITING FOR REVIEW`

## Identity

| Item | Value |
| --- | --- |
| Branch | `cursor/phase0-baseline-audit-02c7` |
| PR | #2 |
| Starting baseline (Phase 6 accepted) | `99b782f8f205c0161c0bba8838d041714e39947e` |
| Final implementation tip | (see PR body / latest green CI) |
| Phase 8 | **Not started** |

## Functional matrix

| Tranche | Verified |
| --- | --- |
| 7.1 Auth / sessions / reconnect / rate limit | Yes (unit + PG + E2E) |
| 7.2 Characters create/list/select/ownership/class seed | Yes |
| 7.3 Inventory / equip / ground pickup concurrency | Yes |
| 7.4 Combat / spells / forged rejection | Yes |
| 7.5 Chat map+global + rate limit + isolation | Yes |
| 7.6 Shop buy/sell + bank | Yes |
| 7.7 XP / level / death / respawn | Yes |
| E2E 17-step + multi-client pickup | Yes (`Phase7E2EGameplayTests`) |

## Security decisions

- PBKDF2-SHA256 passwords; opaque session tokens (hash only stored)
- Generic auth errors; login/chat rate limiting
- Server-authoritative combat/shop/bank; client intentions only
- No compile-time Server→Persistence reference (runtime PG backend)

## Three most important remaining risks

1. Client UI for packets 38–63 is receive-only (events); no dedicated panels yet.
2. Bank gold wallet is in-memory in `ShopBankGameplayService` (item bank is PG-capable).
3. Windows smoke ×3 must stay green under CI load (prior flake mitigations present).

## Reproduction

```bash
dotnet build
dotnet test Frog.Tests/Frog.Tests.csproj
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj
```
