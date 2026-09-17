# Phase 8 — SCREENSHOT_MANIFEST

Windows Phase 8 smoke artifacts from CI run https://github.com/Netsuno/MMO_Maker/actions/runs/35166819419 (capture tip `fa8c44f9`). CI hashes PNGs under `artifacts/phase-08-gameplay-client/` and `artifacts/phase-08-editor/`, writes a temporary table, and **fails** `build-and-test` unless SHA-256 columns and the committed file list match this file exactly (`scripts/verify-phase8-screenshot-manifest.ps1`, gate **exact-sha** for every row). Local/dev refresh: `scripts/update-phase8-screenshot-manifest.ps1`.

Capture was stabilized so exact SHA-256 is required for all 12 files: client `01`/`06` are the gameplay TabControl (no log `[HH:mm:ss]`); editor screenshot smokes pin New/Duplicate GUIDs and focus Save before `DrawToBitmap`. Client `02`–`05` matched across consecutive Windows runs before this pin.

## Client (`artifacts/phase-08-gameplay-client/`)

| Filename | Description | Dimensions | Gate | SHA-256 | Implementation SHA | CI URL |
| --- | --- | --- | --- | --- | --- | --- |
| `01-phase8-tab.png` | Quêtes tab (gameplay TabControl after select + layout; no log clock) | 354×522 | exact-sha | 4d03a5503fe5069a7e19f2ad4516dbd6d8d45b7162972d8e489dfe849d353fae | fa8c44f | https://github.com/Netsuno/MMO_Maker/actions/runs/35166819419 |
| `02-dialogue-choices.png` | Dialogue choices (panel after speaker + choice buttons) | 324×150 | exact-sha | 67c60718bd331adf2299f24a435ae9ea612424a373cbeae9547ca09b3a005403 | fa8c44f | https://github.com/Netsuno/MMO_Maker/actions/runs/35166819419 |
| `03-quest-journal.png` | Quest journal (panel after entries) | 324×80 | exact-sha | 2bf306d5dd744daadcb5c030ee88a1c92fe07b5ca82f89094fa9f76f9d3e614a | fa8c44f | https://github.com/Netsuno/MMO_Maker/actions/runs/35166819419 |
| `04-environment.png` | Region/weather/lighting (panel after Carte: 1) | 324×150 | exact-sha | c0c716391ace82129563dbaac9824d52d26d9fe3b26b247b241ce9393950e67a | fa8c44f | https://github.com/Netsuno/MMO_Maker/actions/runs/35166819419 |
| `05-craft-panel.png` | Craft panel (panel after craft result) | 324×150 | exact-sha | e4fdc42c76ea38cebaff227ce9e9e96d0fde399af0d3b92285b67297414474dc | fa8c44f | https://github.com/Netsuno/MMO_Maker/actions/runs/35166819419 |
| `06-reconnect-usable.png` | Reconnect usability (gameplay TabControl after dialogue restored) | 354×522 | exact-sha | f79742d25cf65e49c4d45abebcced726e7ccf2503d3c341190b4ca1f238fc4ad | fa8c44f | https://github.com/Netsuno/MMO_Maker/actions/runs/35166819419 |

## Editor (`artifacts/phase-08-editor/`)

| Filename | Description | Dimensions | Gate | SHA-256 | Implementation SHA | CI URL |
| --- | --- | --- | --- | --- | --- | --- |
| `01-phase8-content-browse.png` | Contenu Phase 8 browse | 996×679 | exact-sha | dc9e46fb154161f406980cc81c1913ee125bc426887aa07051972d139ecb59f4 | fa8c44f | https://github.com/Netsuno/MMO_Maker/actions/runs/35166819419 |
| `02-dialogue-structured-edit.png` | Structured dialogue editor | 996×679 | exact-sha | 1935a7e40908d1f6cb65ba8fc62210e3f51c463a7504db7a247c5a55844cf8e5 | fa8c44f | https://github.com/Netsuno/MMO_Maker/actions/runs/35166819419 |
| `03-draft-saved.png` | Draft saved | 996×679 | exact-sha | bf0f5f419ee23daab1718a19e31900587341827908df10a85468e302e83ab845 | fa8c44f | https://github.com/Netsuno/MMO_Maker/actions/runs/35166819419 |
| `04-published.png` | Published | 996×679 | exact-sha | de8a7aa277d711bb422d2e905ef44d4332a72fa95cebd3cda12b1b3838d8c012 | fa8c44f | https://github.com/Netsuno/MMO_Maker/actions/runs/35166819419 |
| `05-dirty-discard.png` | Dirty discard | 996×679 | exact-sha | e3c7e4f1b2be4aa5a16959bd8a2667d5b781d83c9c242ce5aea7efda43fa519a | fa8c44f | https://github.com/Netsuno/MMO_Maker/actions/runs/35166819419 |
| `06-close-during-pending.png` | Close during pending op | 996×679 | exact-sha | 4e2d81bd2d14e7c025e5d0b73b9fc76722d850bc1bec2d63a62e8187a6544f65 | fa8c44f | https://github.com/Netsuno/MMO_Maker/actions/runs/35166819419 |

Phase 6/7 screenshots are **not** Phase 8 evidence.

Client `01`≠`02`, `03`≠`04`, and `01`≠`06` on capture tip `fa8c44f9` / CI `35166819419`. All 12 rows are **exact-sha** (byte/exact). `present-dims` is not used in this committed table.
