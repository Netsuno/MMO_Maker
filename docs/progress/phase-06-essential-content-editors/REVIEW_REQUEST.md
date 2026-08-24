# Phase 6 — Review Request (Gate Resubmission)

## Verdict requested

**PHASE 6 GATE REACHED — WAITING FOR REVIEW**

## Branch

`cursor/phase0-baseline-audit-02c7` @ `8fdffc5`

## PR

#2 (Draft)

## Blocker responses

1. **UI smokes** — `GameDataSmokeUiDriver` opens via `CmdGameData`, clicks real buttons, uses search/status (and spawn map/resource) filters, close/reopen
2. **Dirty bind** — `_binding` on Tileset/NPC; `GameDataDirtyStateSmokeTests`
3. **Previews** — `AssetPreviewControl` on 5 categories; resolver tests + preview smoke
4. **Spawn filters** — map + resource catalog comboboxes wired to `MapFilter`/`ResourceFilter`
5. **Init/dispose** — `GameDataInitializationService`, loading UI, cancel on close, `EditorPostgreSqlScope`; leak smoke ×3

## Phase 7

Not started.
