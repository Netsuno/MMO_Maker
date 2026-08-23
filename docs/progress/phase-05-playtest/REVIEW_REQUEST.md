# Demande de revue — Phase 5 (fourth rejection corrections)

## Contexte

Fourth temporary rejection at `ac3a71b2270812567c30f04395b58ad5438faabf`. Corrections on **same** branch/PR only. Phase 6 not started.

## Plage

- After: `ac3a71b2270812567c30f04395b58ad5438faabf`
- Gate HEAD: `81c4ae0fa8be999b007a66d2e0885eac63a59ff6`
- CI: https://github.com/Netsuno/MMO_Maker/actions/runs/32653169799

## Checklist

- [x] Reserved `__frog_playtest__` registration rejected
- [x] Playtest username never falls back to normal account auth
- [x] Atomic claim/commit/release token semantics (failed session creation does not consume)
- [x] TCP tests: reserved reg, first auth, reuse fail, concurrent exactly-one, session-failure no-consume, token not in errors
- [x] Separate `positionMapId` vs `loadedMapId`; READY only when equal (MapData + MapAlreadySynced)
- [x] Negative unit test: PositionUpdate vs MapData mismatch rejects READY
- [x] Real Frog.Client success smoke preserved (Windows ×3)
- [x] Prior third-rejection corrections preserved
- [x] Screenshots remain NOT RUN
