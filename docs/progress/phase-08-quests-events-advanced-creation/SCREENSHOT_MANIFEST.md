# Phase 8 — SCREENSHOT_MANIFEST

Windows Phase 8 smoke artifacts. CI hashes PNGs under `artifacts/phase-08-gameplay-client/` and `artifacts/phase-08-editor/`. `scripts/verify-phase8-screenshot-manifest.ps1` applique le **Gate** de chaque ligne (`exact-sha` ou `present-dims`). Local/dev refresh: `scripts/update-phase8-screenshot-manifest.ps1`.

Capture was stabilized so exact SHA-256 is required for frames that did not change in P10-3a. Client `01` / `05` / `06` include the craft panel (ComboBox noms + bouton Fabriquer) : gate **present-dims** (pixels OS/thème + libellés). `02`–`04` et l’éditeur restent **exact-sha**.

## Client (`artifacts/phase-08-gameplay-client/`)

| Filename | Description | Dimensions | Gate | SHA-256 | Implementation SHA | CI URL |
| --- | --- | --- | --- | --- | --- | --- |
| `01-phase8-tab.png` | Quêtes tab (gameplay TabControl after select + layout; no log clock) | 300–400×250–700 | present-dims | — | P10-3a | |
| `02-dialogue-choices.png` | Dialogue choices (panel after speaker + choice buttons) | 324×150 | exact-sha | 67c60718bd331adf2299f24a435ae9ea612424a373cbeae9547ca09b3a005403 | fa8c44f | https://github.com/Netsuno/MMO_Maker/actions/runs/35166819419 |
| `03-quest-journal.png` | Quest journal (panel after entries) | 324×80 | exact-sha | 2bf306d5dd744daadcb5c030ee88a1c92fe07b5ca82f89094fa9f76f9d3e614a | fa8c44f | https://github.com/Netsuno/MMO_Maker/actions/runs/35166819419 |
| `04-environment.png` | Region/weather/lighting (panel after Carte: 1) | 324×150 | exact-sha | c0c716391ace82129563dbaac9824d52d26d9fe3b26b247b241ce9393950e67a | fa8c44f | https://github.com/Netsuno/MMO_Maker/actions/runs/35166819419 |
| `05-craft-panel.png` | Craft panel (ComboBox noms recettes + Fabriquer) | 300–400×80–220 | present-dims | — | P10-3a | |
| `06-reconnect-usable.png` | Reconnect usability (gameplay TabControl after dialogue restored) | 300–400×250–700 | present-dims | — | P10-3a | |

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

Client `01`≠`02`, `03`≠`04` restent exigés par le smoke C#. `01`/`05`/`06` sont **present-dims** depuis P10-3a (ComboBox recettes). `present-dims` est utilisé pour ces trois lignes client ; le reste de la table reste **exact-sha**.
