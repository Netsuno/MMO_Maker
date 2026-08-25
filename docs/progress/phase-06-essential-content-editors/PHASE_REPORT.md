# Phase 6 — Phase Report (Final targeted fix pass)

## Scope

Essential content editors (tilesets, NPCs, items, spells, classes, shops, resources/spawns) with durable PostgreSQL ports, WinForms lifecycle safety, and CI evidence.

## Implementation SHA

`99b782f8f205c0161c0bba8838d041714e39947e`

## CI

https://github.com/Netsuno/MMO_Maker/actions/runs/32797918806

## Final blocker resolution

| ID | Resolution |
| --- | --- |
| P6-D1 | Async `FormClosing` state machine; cancel → drain → dispose only when complete; timeout keeps form/DB scope alive + retry; real `Close()` smokes (no force-close / no pre-idle wait); STA marshaling on owning thread |
| P6-D2 | CI `upload-artifact@v4`; committed PNGs replaced with exact smoke outputs; `SCREENSHOT_MANIFEST.md` SHA-256 matches artifact |

## Previously accepted (unchanged)

Real PostgreSQL editor scope tests; CRUD/duplicate/save/publish/delete; filters; protected delete; spawn editor; preview clone/refresh; same-branch PR workflow.

## Phase 7

Not started.
