# Phase 9 — KNOWN_ISSUES

## P9-0 (documented, not fixed)

- `PRD_MMO_Maker_CSharp.md` v2.1 is cited but **not in the repo**. P9-S used README / BACKLOG / ADRs instead.
- `docs/DATA_MODEL.md` is incomplete vs `FrogDbContext`.
- `docs/BACKLOG.md` checkboxes stop at Phase 6.
- ~106 historical `// TODO: Implémenter` stubs remain (including `Guild*` / `AdminCommandService`). Not a Phase 9 clear-out.
- CI workflow does not run on this branch until a PR targets `main`.
- No TLS, no backup, no packaging — expected; those are P9-3…P9-4.
- Operator **ACL table exists** (`auth.operators`) but no mute/kick/ban commands yet (P9-1).
- Clear-text TCP if `AllowNonLoopbackBind=true` without an external terminator (`SECURITY_MODEL.md` §10).

## Phase 8 leftovers (not Phase 9 gates)

See [`../phase-08-quests-events-advanced-creation/KNOWN_ISSUES.md`](../phase-08-quests-events-advanced-creation/KNOWN_ISSUES.md): wait-across-disconnect, loot-table editors, arbitrary scripting, MariaDB map-event files.

No Phase 8 regression was run in P9-0. None claimed.

## P9-S

Guilds / groups / trades remain **DEFERRED**.
