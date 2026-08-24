# Phase 6 — Known Issues (Second Fix Pass)

## Fixed in this pass

- Initial Game Data category showed blank host (Tilesets not parented until manual switch)
- In-memory leak smoke could not validate PostgreSQL scope disposal
- Concurrent EF operations on shared `FrogDbContext` from overlapping panel refreshes
- Resource spawn “Toutes les cartes/ressources” filtered on `Guid.Empty`
- Dirty navigation cancel desynchronized list selection on non-tileset panels
- UI smoke matrix incomplete (no duplicate/delete/protected-delete/search on most editors)
- Preview image tied to closed stream; missing checked-in screenshot evidence
- Stale gate documentation referencing older SHA/CI

## Remaining known issues

- Windows `GameDataInitializationLeakSmokeTests` uses in-memory repositories (PostgreSQL lifecycle covered by integration tests)
- Preview screenshots in repo are placeholders until overwritten by Windows smoke on CI
- Shop listing / class spell reference protected-delete smokes use minimal published prerequisites

## Phase 7

Not started.
