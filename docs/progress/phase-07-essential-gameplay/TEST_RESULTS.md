# Phase 7 — Test Results

## Local run (2026-08-26)

| Suite | Count | Result |
| --- | ---: | --- |
| Frog.Tests (all) | 270 | PASS |
| Phase 7 subset | 26 | PASS |

### Phase 7 test classes

- `Phase7AuthTests` (10)
- `Phase7CharacterTests` (3)
- `Phase7InventoryTests` (3)
- `Phase7CombatTests` (3)
- `Phase7ShopBankTests` (2)
- `Phase7ProgressionTests` (3)
- `Phase7E2EGameplayTests` (2) — full TCP E2E + pickup race

### PostgreSQL integration

Run when `FROG_POSTGRES_CONNECTION_STRING` is available:

```bash
dotnet test tests/Frog.Persistence.IntegrationTests
```
