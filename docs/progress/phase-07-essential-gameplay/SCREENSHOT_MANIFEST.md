# Phase 7 — Gameplay client screenshot evidence manifest

Smoke source: `GameplayClientSmokeTests` (Windows STA, in-memory server).

Output directory: `artifacts/phase-07-gameplay-client/`

CI run: https://github.com/Netsuno/MMO_Maker/actions/runs/33036286537  
Artifact: `phase-07-gameplay-client-screenshots`  
Implementation SHA: `c1803132522d8dfb31e3a1284755341eb2d243b2`

All screenshots are 1044×759 PNG. No authentication or reconnect tokens appear in UI logs (verified: `Login OK` / `Reconnect OK` only).

| File | Description | Dimensions | SHA-256 |
|------|-------------|------------|---------|
| `01-login-token-stored.png` | Login OK; token stored privately; log shows `Login OK` only | 1044×759 | `21bf2661790cfb274fd6f724f25fd6f4baf81b93a1a3b746ff5cd0476c125f31` |
| `02-character-created.png` | Character list after create (class Aventurier) | 1044×759 | `279b405d45a833bc73045282ebc3106d4410d05ca1e3aa59c4cedc0e1a5251a8` |
| `03-gameplay-inventory.png` | Playing; named weapon « Épée courte » equipped; shop/bank/ground UI | 1044×759 | `3618b8ed9577f38413dc4d2bbcb51efba1114f7f7f68663e047b021b241169ba` |
| `04-reconnect-ok.png` | Reconnect OK; catalog + character list restored; log shows `Reconnect OK` only | 1044×759 | `764e2a16052c24afdf4255a2923cc12b3d977e41c95b639da3b605985fabf8f1` |
| `05-reconnect-gameplay-usable.png` | Re-enter game; equipped weapon persisted; shop/bank/ground usable | 1044×759 | `97bf7ab3e68a18f94cd31b9b0301615643f60f5e56be31d1f448cce104f5749d` |
