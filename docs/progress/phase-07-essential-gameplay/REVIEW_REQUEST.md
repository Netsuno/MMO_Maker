# Phase 7 — Review Request

## Ready for review

`PHASE 7 GATE REACHED — WAITING FOR REVIEW`

## Identity

| Item | Value |
| --- | --- |
| Branch | `cursor/phase0-baseline-audit-02c7` |
| PR | #2 |
| Starting baseline (Phase 6 accepted) | `99b782f8f205c0161c0bba8838d041714e39947e` |
| Final tip SHA | `33ec0dce22c2b54ce325541bba1a9b68fad1b768` |
| Final CI | https://github.com/Netsuno/MMO_Maker/actions/runs/32921656173 |
| Phase 8 | **Not started** |

## Functional matrix

| Tranche | Verified |
| --- | --- |
| 7.1 Auth / sessions / reconnect / rate limit | Yes |
| 7.2 Characters create/list/select/ownership/class seed | Yes |
| 7.3 Inventory / equip / ground pickup concurrency | Yes |
| 7.4 Combat / spells / forged rejection | Yes |
| 7.5 Chat map+global + rate limit + isolation | Yes |
| 7.6 Shop buy/sell + bank | Yes |
| 7.7 XP / level / death / respawn | Yes |
| E2E 17-step + multi-client pickup | Yes |

## Security decisions

- PBKDF2-SHA256; opaque session tokens (hash only); generic auth errors
- Login/chat/reconnect rate limiting; reconnect displaces stale session
- Server-authoritative combat/shop/bank

## Three most important remaining risks

1. Dedicated client UI panels for inventory/combat/shop are receive-events only.
2. Bank gold wallet path is partially in-memory (`ShopBankGameplayService`).
3. Windows smoke remains sensitive to STA dispatcher timing under load.

## Reproduction

```bash
dotnet build -c Release
dotnet test Frog.Tests/Frog.Tests.csproj -c Release
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release
```
