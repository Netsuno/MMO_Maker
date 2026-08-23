# Phase 6 — KNOWN ISSUES

1. Map tiles still reference tilesets via integer `EditorPaletteId` embedded in cell JSON (not Guid FK).
2. Map NPC spawns still use legacy integer `npc_definition_id` / `EditorAliasId` bridge.
3. Sprite/icon file preview in Game Data forms is minimal (logical path + metadata; not full asset browser).
4. Playtest graphical screenshots remain **NOT RUN** (Phase 5 carry-over).
5. Phase 7 gameplay (combat, inventory, shop transactions, harvesting simulation) not started by design.
6. MariaDB remains on the runtime server auth path as temporary legacy.
