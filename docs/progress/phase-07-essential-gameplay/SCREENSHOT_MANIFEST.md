# Phase 7 — Gameplay client screenshot evidence manifest

Smoke source: `GameplayClientSmokeTests` (Windows STA, in-memory server).

Output directory: `artifacts/phase-07-gameplay-client/`

CI run: pending (implementation tip `fefa07080583c4cc53f16fd52f1606bac90b1442`)
Artifact: `phase-07-gameplay-client-screenshots`
Implementation SHA: `fefa07080583c4cc53f16fd52f1606bac90b1442`

All screenshots are 1044×759 PNG unless noted. No authentication or reconnect tokens appear in UI logs (verified: `Login OK` / `Reconnect OK` only).

| File | Description | Dimensions | SHA-256 |
|------|-------------|------------|---------|
| `01-login-token-stored.png` | Login OK; token stored privately; log shows `Login OK` only | 1044×759 | prior CI `21bf2661790cfb274fd6f724f25fd6f4baf81b93a1a3b746ff5cd0476c125f31` |
| `02-character-created.png` | Character list after create (class Aventurier) | 1044×759 | prior CI `279b405d45a833bc73045282ebc3106d4410d05ca1e3aa59c4cedc0e1a5251a8` |
| `03-gameplay-inventory.png` | Playing; named weapon equipped; shop/bank/ground UI | 1044×759 | prior CI `3618b8ed9577f38413dc4d2bbcb51efba1114f7f7f68663e047b021b241169ba` |
| `04-reconnect-ok.png` | Reconnect OK; catalog + character list restored | 1044×759 | prior CI `764e2a16052c24afdf4255a2923cc12b3d977e41c95b639da3b605985fabf8f1` |
| `05-reconnect-gameplay-usable.png` | Re-enter game; equipment persisted; shop/bank/ground usable | 1044×759 | prior CI `97bf7ab3e68a18f94cd31b9b0301615643f60f5e56be31d1f448cce104f5749d` |
| `06-bank-shop.png` | Bank item deposit + shop buy via visible inventory selection | 1044×759 | pending CI |
| `07-ground-drop.png` | Ground drop after inventory selection | 1044×759 | pending CI |
| `08-combat-spell.png` | Melee hit + spell success against seeded Slime | 1044×759 | pending CI |
| `09-death-respawn-button.png` | PvP death; visible enabled Respawn button | 1044×759 | pending CI |
| `10-post-respawn.png` | Post-respawn HP/MP restored | 1044×759 | pending CI |
