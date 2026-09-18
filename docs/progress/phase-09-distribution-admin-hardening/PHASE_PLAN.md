# Phase 9 — Delivery plan (P9-0)

**Branch:** `cursor/phase9-distribution-admin-hardening`
**Base / audit tip:** `5af47b9cf6ba18a82dba5eee933fc1d0e6afa3eb` (`main` tip — README merge PR #6)
**Baseline CI (main only):** https://github.com/Netsuno/MMO_Maker/actions/runs/35286923042 SUCCESS
**This document:** planning only. No feature implementation in P9-0.

Phase 8 is **ACCEPTED on main** (merge `1cd57ba`). Phase 9 is **IN PROGRESS** on this branch. Do not start Phase 10. Do not change Phase 8 product behavior except documenting regressions found during later implementation (none claimed here).

Product authority used for this plan (in order):

1. In-repo docs that claim alignment with `PRD_MMO_Maker_CSharp.md` v2.1: [`README.md`](../../../README.md), [`docs/BACKLOG.md`](../../BACKLOG.md), [`docs/ARCHITECTURE.md`](../../ARCHITECTURE.md)
2. ADRs: [`ADR-0002`](../../decisions/ADR-0002-postgresql-source-of-truth.md), [`ADR-0003`](../../decisions/ADR-0003-frog-inspiration-no-compatibility.md), [`ADR-0004`](../../decisions/ADR-0004-editor-wpf-shell-temporary.md)
3. Phase 8 acceptance notes: [`KNOWN_ISSUES.md`](../phase-08-quests-events-advanced-creation/KNOWN_ISSUES.md)

**PRD file status:** `PRD_MMO_Maker_CSharp.md` v2.1 is cited as authority but is **not in this repository** (no path, no git history, not in GitHub tree). This plan does not invent PRD section numbers. Prefer ADR + README/BACKLOG over VB6 / `Frog.Legacy` / historical stubs.

---

## P9-S — Social decision (guilds / groups / trades)

**Decision: DEFERRED.** Not in Phase 9 scope. Do not implement. Do not pretend delivered.

| Claim | Evidence |
| --- | --- |
| Phase 9 title in README | Packaging, admin (mute/kick/ban), prod security, load certification — [`README.md`](../../../README.md) roadmap table |
| Chat product decision | “Global / map / whisper. **Guilde / groupe plus tard.**” — [`README.md`](../../../README.md) product table |
| Phase 8 leftover | “Phase 9 packaging, admin moderation, load certification” — Phase 8 [`KNOWN_ISSUES.md`](../phase-08-quests-events-advanced-creation/KNOWN_ISSUES.md) |
| Active backlog | [`docs/BACKLOG.md`](../../BACKLOG.md) has no guild / group / trade items (stops at Phase 6 checkboxes; Phase 7–8 accepted on main) |
| Wire protocol today | `ChatChannel` = Global / Map / Whisper only (`Frog.Core/Enums/ChatChannel.cs`). `PacketId` has no guild / party / trade opcodes (`Frog.Core/Enums/PacketId.cs`) |
| Historical stubs | `Frog.Server/Models/Guild.cs`, `Frog.Server/Services/GuildService.cs` are unused `// TODO: Implémenter` skeletons — **not** product features (ADR-0003: ignore VB6-shaped leftovers) |

Marc’s GO: include guilds/groups/trades **only if** the current PRD assigns them to Phase 9. The PRD file is absent; every in-repo PRD-aligned source assigns Phase 9 to distribution / admin / hardening, and places guilds/groups **later**. Player-to-player **trade** is not assigned to Phase 9 either (shop/bank already shipped in Phase 7).

UDP / AOI is listed in README as “plus tard (Phase 9+ / observabilité)” — that is **not** a Phase 9 delivery item. Measure load on the current TCP server; do not build UDP or AOI here.

---

## Scope

| ID | Name | Goal |
| --- | --- | --- |
| **P9-0** | Audit + delivery plan | This folder, STATUS bootstrap, P9-S decision. **This task.** |
| **P9-1** | Admin / moderation | Operator mute / kick / ban on the authoritative TCP server, persisted in PostgreSQL, enforced on chat and session. |
| **P9-2** | Security / permissions | Production threat model: who may moderate, secret handling, bind/TLS, leftover dangerous packets, auth hardening. |
| **P9-3** | PostgreSQL backup / restore | Versioned backup + restore runbook against the real EF schema; prove a restored DB boots the server. |
| **P9-4** | Packaging | Reproducible Windows client/editor + Linux-capable server layout; no new MariaDB; no `.fcc` importer. |
| **P9-5** | Load / observability | Measure (do not guess) TCP + PG thresholds; add enough ops signals to certify a small hosted world. |
| **P9-6** | Tests + final review | Gate evidence, E2E matrix, review request. No Phase 10. |
| **P9-S** | Social (guilds / groups / trades) | **DEFERRED** — see above. |

---

## Order of work and dependencies

```text
P9-0 (docs) ──► P9-2 security model (doc + permission design)
                 │
                 ├──► P9-1 admin/moderation (needs permission model)
                 │
                 ├──► P9-3 backup/restore (parallel after P9-0; schema frozen unless P9-1 needs ban tables)
                 │
                 └──► P9-4 packaging (after P9-2 config/secret decisions)
                          │
                          └──► P9-5 load/observability (needs a packaged, startable server)
                                   │
                                   └──► P9-6 tests + review docs
```

Rules:

- P9-2 **permission / threat-model write-up** must exist before P9-1 ships mute/kick/ban on the public TCP surface.
- P9-1 may add PostgreSQL tables (`auth` or `ops` — decide in P9-2). Those land **before** P9-3 restore proofs if they change the schema.
- P9-3 can start in parallel on the current 24-migration schema if P9-1 tables are not ready; re-run restore after any new migration.
- P9-4 must not introduce MariaDB or VB6 import.
- P9-5 proposed numbers in [`BASELINE_AUDIT.md`](BASELINE_AUDIT.md) are **not measurements**. P9-5 owns measurement.
- P9-6 is last. Do not declare Phase 9 READY without P9-1…P9-5 evidence.

---

## Out of scope (Phase 9)

- Guilds, parties/groups, player-to-player trade windows (P9-S deferred)
- UDP snapshots / AOI (README “later”, not this phase)
- Map instances / sharding
- Arbitrary Lua / C# / PowerShell author scripts (Phase 8 already rejected)
- VB6 / `.fcc` import, `Frog.LegacyImporter`, FRoG protocol parity (ADR-0003)
- New MariaDB tables or a new MariaDB dependency (ADR-0002)
- Speculative Phase 8 gameplay edits (quests, events, interact identity)
- Phase 10
- Rewriting the editor off WPF (ADR-0004 still temporary)
- Clearing ~106 historical `// TODO: Implémenter` skeleton files

---

## Hard constraints (Marc / Orchestrator)

- Commits only on `cursor/phase9-distribution-admin-hardening`
- Do not create a new remote branch, merge, rebase, or force-push
- Orchestrator owns the single draft PR
- No Phase 10
- PostgreSQL remains the product source of truth
