# Phase 7 — Gameplay client screenshot evidence manifest

Smoke source: `GameplayClientSmokeTests` (Windows STA, in-memory server).

Output directory: `artifacts/phase-07-gameplay-client/`

CI run: https://github.com/Netsuno/MMO_Maker/actions/runs/33138380861
Artifact: `phase-07-gameplay-client-screenshots`
Implementation SHA: `bfa86bafa1d367a8ab0127c2fff352113b439d65`

All screenshots are 1044×759 PNG. No authentication or reconnect tokens appear in UI logs (verified: `Login OK` / `Reconnect OK` only).

| File | Description | Dimensions | SHA-256 |
|------|-------------|------------|---------|
| `01-login-token-stored.png` | Login OK; token stored privately; log shows `Login OK` only | 1044×759 | `72bae347597cb2c9ea8af0d87ad15c6acb5f88fe72847156973c68e983716e96` |
| `02-character-created.png` | Character list after create (class Aventurier) | 1044×759 | `a04dee07bca98d45f93643f57d9b099bd241037b3f898ee3812d7f3befd63ba2` |
| `03-gameplay-inventory.png` | Playing; named weapon equipped; shop/bank/ground UI | 1044×759 | `66aba561be6076a0e544e9382ed3ee065779d735666522585071ad830a40682a` |
| `04-reconnect-ok.png` | Reconnect OK; catalog + character list restored | 1044×759 | `6ab9b196f1b582f507f8bc3bc38ff8af53e5d7aeb791f4179dd5b869111f5a9b` |
| `05-reconnect-gameplay-usable.png` | Re-enter game; equipment persisted; shop/bank/ground usable | 1044×759 | `a98546181e9dc9f00c5b2098a03ef922e3f02dc22848bcb6067680fc544b1ba3` |
| `06-bank-shop.png` | Bank item deposit + shop buy via visible inventory selection | 1044×759 | `e3d9fe266c459029fe2dfdfd2561d4159d0c204722cf6e056f15af15f0a0e322` |
| `07-ground-drop.png` | Ground drop after inventory selection | 1044×759 | `24789fe8169220b982dbcd4a673c37108abcdd0c4fa2d9e14c3d68252f08913e` |
| `08-combat-spell.png` | Melee hit + spell success against seeded Slime | 1044×759 | `2bcbc4a8cb725ac1ee471507b7f2c616b08baacbe4cd33c86697e8ae087dfd8a` |
| `09-death-respawn-button.png` | PvP death; visible enabled Respawn button | 1044×759 | `3eefd6cea1a5f168b9261858d21b66c775b2f46abfa09bf5316b4283883dfa09` |
| `10-post-respawn.png` | Post-respawn HP/MP restored | 1044×759 | `fcc1b92dd9d64084455e2ff2203620282fb1e4ee9727a5133f6cea52a8d6c4d5` |
