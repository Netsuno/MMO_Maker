# État du projet — MMO Maker

- Dernière mise à jour : 2026-08-23
- Branche : `cursor/phase0-baseline-audit-02c7`
- PR : #2 (Draft)
- **Phase 4 : ACCEPTED** — `22d19b4570eaf552e5ce162243a83020ce86e2eb`
- **Phase 5 : ACCEPTED** — `1944d73b6fffa84799d288da555f1005b82f2698`
- **Phase 6 : IN PROGRESS** — essential content editors
  - Slice 1 Tilesets : **COMPLETE** (see `docs/progress/phase-06-essential-content-editors/SLICE_01_TILESETS.md`)
  - Slice 2 NPCs/monsters : **COMPLETE** (see `docs/progress/phase-06-essential-content-editors/SLICE_02_NPCS.md`)
  - Slice 3 Items : **COMPLETE** (see `docs/progress/phase-06-essential-content-editors/SLICE_03_ITEMS.md`)
  - Slice 4 Spells/skills : **COMPLETE** (see `docs/progress/phase-06-essential-content-editors/SLICE_04_SPELLS.md`)
  - Remaining : Classes → Shops → Resources/spawns
- Phase 7 : not started

## Local verification (Phase 6 through slice 4)

| Suite | Passed |
| --- | ---: |
| Frog.Tests | 223 |
| PostgreSQL | 26 |

## Known issues carried forward

- Playtest screenshots NOT RUN (Phase 5)
- Tileset↔map references via EditorPaletteId JSON (not Guid FK yet)
- NPC↔map spawn references use legacy integer `EditorAliasId` (not Guid FK yet)
- Legacy integer gameplay items are not yet migrated to `ItemDefinition` GUID references
- Spell effects and gameplay execution are not yet wired to `SpellDefinition`
