# Phase 8 — SCREENSHOT_MANIFEST

Windows Phase 8 smoke artifacts. CI hashes PNGs under `artifacts/phase-08-gameplay-client/` and `artifacts/phase-08-editor/`, writes a temporary table, and **fails** `build-and-test` unless SHA-256 columns and the committed file list match this file exactly (`scripts/verify-phase8-screenshot-manifest.ps1`). Local/dev refresh: `scripts/update-phase8-screenshot-manifest.ps1`.

Implementation tip: see git tip of `cursor/phase0-baseline-audit-02c7` (R2-6). CI: see tip CI.

## Client (`artifacts/phase-08-gameplay-client/`)

| Filename | Description | Dimensions | SHA-256 | Implementation SHA | CI URL |
| --- | --- | --- | --- | --- | --- |
| `01-phase8-tab.png` | Quêtes tab (full shell after tab select + layout) | 1044×759 | a46b009c5bdaed80bd57c10d1cbcf1efb739fd11cbacc487dde722a96c868236 | see-tip | see tip CI |
| `02-dialogue-choices.png` | Dialogue choices (panel after speaker + choice buttons) | 1044×759 | a46b009c5bdaed80bd57c10d1cbcf1efb739fd11cbacc487dde722a96c868236 | see-tip | see tip CI |
| `03-quest-journal.png` | Quest journal (panel after entries) | 1044×759 | d7347461a9fe4506ade8b4cd4c8dff5e03e8e99d7878002668ccb8996c501a14 | see-tip | see tip CI |
| `04-environment.png` | Region/weather/lighting (panel after Carte: 1) | 1044×759 | d7347461a9fe4506ade8b4cd4c8dff5e03e8e99d7878002668ccb8996c501a14 | see-tip | see tip CI |
| `05-craft-panel.png` | Craft panel (panel after craft result) | 1044×759 | 9f05041b0ea8875f02c6bcde31c50a64e5f1c69aac900fba96a4f4ff6a9ab4cd | see-tip | see tip CI |
| `06-reconnect-usable.png` | Reconnect usability | 1044×759 | 44524fc471985aed0c3f986124f3d285f3c0d02417414b33b5af7ae41cba5787 | see-tip | see tip CI |

## Editor (`artifacts/phase-08-editor/`)

| Filename | Description | Dimensions | SHA-256 | Implementation SHA | CI URL |
| --- | --- | --- | --- | --- | --- |
| `01-phase8-content-browse.png` | Contenu Phase 8 browse | 996×679 | 385c618cd77bf1c902149d0a92b99d96c1fcff490f8b85413e954fe9ed56cc28 | see-tip | see tip CI |
| `02-dialogue-structured-edit.png` | Structured dialogue editor | 996×679 | dbe594815b580ebee6d8dc0b4948ab50de3e34cd1318980e2dcbb767e591db8e | see-tip | see tip CI |
| `03-draft-saved.png` | Draft saved | 996×679 | b7263421070e834ca1d60fc156e617d66ef7afa54b31f256ac3e99fce4210806 | see-tip | see tip CI |
| `04-published.png` | Published | 996×679 | 8e7cac51a9e3fd0feaf07274f20084409fa1f7330adcd452ca4eec277e051478 | see-tip | see tip CI |
| `05-dirty-discard.png` | Dirty discard | 996×679 | 88fa60db764cab90a65f0e5eb19fc816a0afe6445f823a1e8203e7f2664b8eb2 | see-tip | see tip CI |
| `06-close-during-pending.png` | Close during pending op | 996×679 | 8deb3bbeb7895f62aff8589d68b3eb425052672f0be5f434052ccc54a26c2109 | see-tip | see tip CI |

Phase 6/7 screenshots are **not** Phase 8 evidence.

Client `02`–`05` are panel surfaces captured after the claimed UI state is observed (so `01`≠`02` and `03`≠`04` even when enter-game already pushed dialogue/environment before the Quêtes tab is selected). SHA-256 values above are the last known Windows smoke hashes and must be refreshed from tip CI artifacts so they match the PNGs produced by the distinct-frame captures.
