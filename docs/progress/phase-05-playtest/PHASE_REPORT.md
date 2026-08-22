# Phase 5 — Client/server playtest — PHASE REPORT

## Status

**PHASE 5 GATE REACHED — WAITING FOR REVIEW**

- Phase 4 accepted: `22d19b4570eaf552e5ce162243a83020ce86e2eb`
- Phase 5 green head: `e2a1c0c179d5c2189ec2ef58d7dd945856c7678d`
- CI: https://github.com/Netsuno/MMO_Maker/actions/runs/32590970105
- Branch: `cursor/phase0-baseline-audit-02c7` (PR #2)
- Phase 6: **not started**

## Objective delivered

Reliable playtest of an **explicitly published** PostgreSQL map:

1. Validate → save dirty draft → publish (or pin existing published revision).
2. Never playtest unsaved in-memory-only changes.
3. Editor starts local test server + client with correlated IDs.
4. Server loads published MapId + revision via playtest manifest (`.fmap` blobs).
5. Unpublished drafts never sent to server/client (no PG connection string on playtest processes).
6. Configurable spawn tile on playtest login.
7. Server authoritative for movement, collision, warps.
8. Client map/runtime data via TCP protocol only.
9. Correlated editor/server logs + actionable errors.
10. Editor stops only processes it started; cleanup on cancel, failure, form close.

## CI counts

| Suite | Result |
| --- | --- |
| Frog.Tests | 145/145 |
| PostgreSQL integration | 17/17 |
| Windows smoke | 9/9 × 3 |

## Evidence pack

Sibling files in this directory + `playtest-launch.png`, `playtest-client-running.png`.
