# État du projet — MMO Maker

- Dernière mise à jour : 2026-08-25
- Branche : `cursor/phase0-baseline-audit-02c7`
- PR : #2 (Draft)
- **Phase 4 : ACCEPTED** — `22d19b4570eaf552e5ce162243a83020ce86e2eb`
- **Phase 5 : ACCEPTED** — `1944d73b6fffa84799d288da555f1005b82f2698`
- **Phase 6 : ACCEPTED**
  - Accepted implementation SHA : `99b782f8f205c0161c0bba8838d041714e39947e`
  - Accepted evidence baseline (branch tip) : `f4db56592346d9bf0cad9ca153aaeff11ee65de8`
  - Evidence : `docs/progress/phase-06-essential-content-editors/`
  - Screenshot manifest : `docs/progress/phase-06-essential-content-editors/SCREENSHOT_MANIFEST.md`
  - CI (implementation) : https://github.com/Netsuno/MMO_Maker/actions/runs/32797918806
  - CI (final tip) : https://github.com/Netsuno/MMO_Maker/actions/runs/32798293788
  - Screenshot artifact : `phase-06-game-data-screenshots` (run 32797918806)
- **Phase 7 : IN PROGRESS** (tranche 7.1 — Authentication and sessions)
  - Evidence dossier : `docs/progress/phase-07-essential-gameplay/`
  - Starting from accepted Phase 6 implementation SHA `99b782f`
  - 7.1 : auth ports, PBKDF2 hashing, PG `auth` schema, sessions, reconnect protocol

## CI counts (latest local run — 7.1 tranche)

| Suite | Count |
| --- | ---: |
| Frog.Tests (unit) | 254 |
| PostgreSQL integration | 39 |
| Windows smoke | 35 × 3 (Phase 6 baseline; not re-run for 7.1) |

## Known issues

- Phase 6 : `docs/progress/phase-06-essential-content-editors/KNOWN_ISSUES.md`
- Phase 7 : `docs/progress/phase-07-essential-gameplay/KNOWN_ISSUES.md` (when present)

## Phase 8

Not started.
