# Phase 04 — résumé des changements (gate data safety)

## Plage

- Gate Phase 3 : `20eedc1`
- Phase 4 : `20eedc1..HEAD` (`08ff2e6230f78d832e72dbe101007a3cf3c00f30`)

## Persistence / contrat

- `MapId = null` → création ; id retourné ; mise à jour si existant
- Draft/publish séparés ; concurrence atomique ; fixtures warp valides
- EF `ChangeTracker.Clear()` après `ExecuteUpdate`

## Éditeur / smoke

- Hôte STA unique partagé ; `DisableTestParallelization`
- Fermeture WPF : cancel synchrone + prompt `ApplicationIdle`
- `MapCanvas` → `MapEditOperations` ; tileset via `TilesetCache.LoadFromFile`
- Undo snapshot avant mutation ; assertions sur `canvas.Map`
- CI : smoke Windows exécuté 3 fois de suite

## Preuve CI

https://github.com/Netsuno/MMO_Maker/actions/runs/32585492554 — 129 / 16 / 7×3 PASS
