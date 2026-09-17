# Phase 8 — SCREENSHOT_MANIFEST

Windows Phase 8 smoke PNGs under `artifacts/phase-08-gameplay-client/` and `artifacts/phase-08-editor/`. CI runs `scripts/verify-phase8-screenshot-manifest.ps1` after the ×3 Phase 8 smokes (does **not** rewrite this file). Local/dev refresh of **exact-sha** hashes: `scripts/update-phase8-screenshot-manifest.ps1`.

## Gate policy

Refreshing SHA-256 from a single CI run is **not** a fix for frames whose pixels drift across consecutive Windows runners. Cross-run compare of CI `35165209463` (tip `798743fa`) vs `35165948678` (tip `cb7c158`):

| Set | Files | Gate | Why |
| --- | --- | --- | --- |
| Stable | client `02`–`05` (panel crops after claimed UI state), editor `01` (browse, no generated id) | **exact-sha** | SHA-256 + exact WxH matched across those runs |
| Unstable | client `01`/`06` (previously full shell with `[HH:mm:ss]` log + random account/port), editor `02`–`06` (full dialog: generated ids, caret/focus/GDI) | **present-dims** | file present + dimension spec; SHA recorded in the generated table only |

`present-dims` still **fails** CI when a required PNG is missing, not a valid PNG, or the wrong size (so a 1×1 stub or a 324×150 panel captured as a “tab shell” is rejected). Exact SHA-256 is **not** required for that set. Capture is still stabilized where it helps: client `01`/`06` are the gameplay **TabControl** (no log clock); editor screenshot smokes use deterministic New/Duplicate GUIDs + WaitForPaint. Those pixels are still not SHA-gated.

Dimension specs:

- `W×H` — exact pixels (stable panels / editor browse / editor full windows).
- `W1–W2×H1–H2` — inclusive range. Client `01`/`06` are the 360px gameplay TabControl (not the 1044×759 shell): wide enough to be that column, tall enough not to be a single 324×150 panel.

Distinct-frame guarantees (always on actual artifact bytes, independent of gate): client `01`≠`02` and `03`≠`04`.

## Client (`artifacts/phase-08-gameplay-client/`)

| Filename | Description | Dimensions | Gate | SHA-256 | Implementation SHA | CI URL |
| --- | --- | --- | --- | --- | --- | --- |
| `01-phase8-tab.png` | Quêtes tab (gameplay TabControl after select + dialogue + environment; no log clock) | 300–400×250–700 | present-dims | — | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `02-dialogue-choices.png` | Dialogue choices (panel after speaker + choice buttons) | 324×150 | exact-sha | 67c60718bd331adf2299f24a435ae9ea612424a373cbeae9547ca09b3a005403 | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `03-quest-journal.png` | Quest journal (panel after entries) | 324×80 | exact-sha | 2bf306d5dd744daadcb5c030ee88a1c92fe07b5ca82f89094fa9f76f9d3e614a | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `04-environment.png` | Region/weather/lighting (panel after Carte: 1) | 324×150 | exact-sha | c0c716391ace82129563dbaac9824d52d26d9fe3b26b247b241ce9393950e67a | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `05-craft-panel.png` | Craft panel (panel after craft result) | 324×150 | exact-sha | e4fdc42c76ea38cebaff227ce9e9e96d0fde399af0d3b92285b67297414474dc | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `06-reconnect-usable.png` | Reconnect usability (gameplay TabControl after dialogue restored) | 300–400×250–700 | present-dims | — | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |

## Editor (`artifacts/phase-08-editor/`)

| Filename | Description | Dimensions | Gate | SHA-256 | Implementation SHA | CI URL |
| --- | --- | --- | --- | --- | --- | --- |
| `01-phase8-content-browse.png` | Contenu Phase 8 browse | 996×679 | exact-sha | 385c618cd77bf1c902149d0a92b99d96c1fcff490f8b85413e954fe9ed56cc28 | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `02-dialogue-structured-edit.png` | Structured dialogue editor | 996×679 | present-dims | — | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `03-draft-saved.png` | Draft saved | 996×679 | present-dims | — | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `04-published.png` | Published | 996×679 | present-dims | — | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `05-dirty-discard.png` | Dirty discard | 996×679 | present-dims | — | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |
| `06-close-during-pending.png` | Close during pending op | 996×679 | present-dims | — | 798743f | https://github.com/Netsuno/MMO_Maker/actions/runs/35165209463 |

Phase 6/7 screenshots are **not** Phase 8 evidence.

Client `02`–`05` are panel surfaces captured after the claimed UI state is observed (stable SHA-256). Client `01`/`06` are the gameplay TabControl (not the full shell) so log timestamps / random account / ephemeral port are excluded; pixels are still not SHA-gated. Editor `01` is the empty browse window (stable). Editor `02`–`06` keep the full 996×679 dialog (status/list/name are the claimed state) with presence+exact size only; screenshot smokes pin New/Duplicate GUIDs.
