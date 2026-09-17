# Phase 8 — SCREENSHOT_MANIFEST

Windows Phase 8 smoke artifacts from CI run https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 (capture tip `798743fa`). CI hashes PNGs under `artifacts/phase-08-gameplay-client/` and `artifacts/phase-08-editor/`, writes a temporary table, and **fails** `build-and-test` unless SHA-256 columns and the committed file list match this file exactly (`scripts/verify-phase8-screenshot-manifest.ps1`). Local/dev refresh: `scripts/update-phase8-screenshot-manifest.ps1`.

## Client (`artifacts/phase-08-gameplay-client/`)

| Filename | Description | Dimensions | SHA-256 | Implementation SHA | CI URL |
| --- | --- | --- | --- | --- | --- |
| `01-phase8-tab.png` | Quêtes tab (gameplay TabControl after select + layout; no log clock) | 1044×759 | 0575cab5aae991ec90b77e9cd17cdd90a8f67d4bd821ce08f9cccb8a870b2d83 | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `02-dialogue-choices.png` | Dialogue choices (panel after speaker + choice buttons) | 324×150 | 67c60718bd331adf2299f24a435ae9ea612424a373cbeae9547ca09b3a005403 | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `03-quest-journal.png` | Quest journal (panel after entries) | 324×80 | 2bf306d5dd744daadcb5c030ee88a1c92fe07b5ca82f89094fa9f76f9d3e614a | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `04-environment.png` | Region/weather/lighting (panel after Carte: 1) | 324×150 | c0c716391ace82129563dbaac9824d52d26d9fe3b26b247b241ce9393950e67a | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `05-craft-panel.png` | Craft panel (panel after craft result) | 324×150 | e4fdc42c76ea38cebaff227ce9e9e96d0fde399af0d3b92285b67297414474dc | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `06-reconnect-usable.png` | Reconnect usability (gameplay TabControl after dialogue restored) | 1044×759 | 135d6ade6cbaf2fee4ccbd76ee923ab30c627d9cafbda4d3f40ff508a449da83 | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |

## Editor (`artifacts/phase-08-editor/`)

| Filename | Description | Dimensions | SHA-256 | Implementation SHA | CI URL |
| --- | --- | --- | --- | --- | --- |
| `01-phase8-content-browse.png` | Contenu Phase 8 browse | 996×679 | 385c618cd77bf1c902149d0a92b99d96c1fcff490f8b85413e954fe9ed56cc28 | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `02-dialogue-structured-edit.png` | Structured dialogue editor | 996×679 | 97b231dbb330abf5c72613dfc70d11eb2f765f0c7ecea701c1d566ddfc32ceb5 | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `03-draft-saved.png` | Draft saved | 996×679 | 5f18e816f6664fd31c06696786517d144a2b508ab17a50c26d55f3b954428c18 | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `04-published.png` | Published | 996×679 | 48a3fd412c4de1af85b6ca9a91a6ce9156fc502673bdafcb38cfe05bc9f81b20 | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `05-dirty-discard.png` | Dirty discard | 996×679 | a7ffb4ffaf61bb07f9ca4e8d21581f1aa06a2008d31f5e4f92d9049cd8d841da | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `06-close-during-pending.png` | Close during pending op | 996×679 | 2a1b670c1a9fb41baf0f397a0db702b3acdc31aa7b1b4f4cbc663b2be4623ce8 | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |

Phase 6/7 screenshots are **not** Phase 8 evidence.

Client `01`≠`02` and `03`≠`04` on capture tip `798743fa` / CI `35165209463`. Client `02`–`05` are panel surfaces captured after the claimed UI state is observed. Editor `01-phase8-content-browse.png` SHA-256 is unchanged vs the prior manifest; other files were refreshed from this tip’s smoke artifacts.
