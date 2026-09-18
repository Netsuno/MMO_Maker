# Phase 9 — KNOWN_ISSUES

## P9-0 (documented, not fixed)

- `PRD_MMO_Maker_CSharp.md` v2.1 is cited but **not in the repo**. P9-S used README / BACKLOG / ADRs instead.
- `docs/DATA_MODEL.md` is incomplete vs `FrogDbContext`.
- `docs/BACKLOG.md` checkboxes stop at Phase 6.
- ~106 historical `// TODO: Implémenter` stubs remain (including `Guild*`). `AdminCommandService` / `Role` / `Permission` / `AccessRightEnum` are unused folklore (P9-1 uses `ModerationService`). Not a Phase 9 clear-out.
- CI workflow does not run on this branch until a PR targets `main`.
- No TLS, packaging still TBD (P9-4). Backup/restore scripts exist (P9-3); **re-run restore proofs after the P9-1 ops sanction migration**.
- Operator mute/kick/ban is implemented (P9-1). Grant remains out-of-band SQL (`auth.operators`).
- Clear-text TCP if `AllowNonLoopbackBind=true` without an external terminator (`SECURITY_MODEL.md` §10).

## Phase 8 leftovers (not Phase 9 gates)

See [`../phase-08-quests-events-advanced-creation/KNOWN_ISSUES.md`](../phase-08-quests-events-advanced-creation/KNOWN_ISSUES.md): wait-across-disconnect, loot-table editors, arbitrary scripting, MariaDB map-event files.

No Phase 8 regression was run in P9-0. None claimed.

## P9-S

Guilds / groups / trades remain **DEFERRED**.
