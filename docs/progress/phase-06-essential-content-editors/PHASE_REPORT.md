# Phase 6 — Essential Content Editors (Second Fix Pass)

## Status

**GATE REACHED — WAITING FOR REVIEW**

## Branch tip

`3d8ff73044f72963836ad8cb0bf43c1870811f50`

## Implementation SHA

`3d8ff73` — reviewed code for P6-B1…B8 (documentation-only commits may follow on branch tip)

## CI

https://github.com/Netsuno/MMO_Maker/actions/runs/32785847198

## Second-pass blocker fixes

| Blocker | Fix |
| --- | --- |
| P6-B1 Initial category blank | `ShowInitialCategory()` after `_initialized = true`; smoke asserts tileset panel visible without manual category change |
| P6-B2 DbContext leak smoke ineffective | `PostgresGameDataScopeLifecycleTests` on real PostgreSQL (migrate once, dispose, drain, repeated open/close) |
| P6-B3 Shared DbContext concurrency | Reentrant `FrogDbContextGate` serializes EF operations; `GameDataPanelAsyncGate` serializes panel UI async; form close drains panels + scope |
| P6-B4 “Toutes…” spawn filters | `NormalizeFilterId` converts `Guid.Empty` → `null`; spawn filter UI smoke |
| P6-B5 Dirty navigation desync | `GameDataListNavigation` helper on all panels (revert list selection, preserve dirty edits) |
| P6-B6 UI smoke coverage | Full matrix per editor: dup/delete, invalid publish, search/filter, close/reopen, cancel nav, protected delete |
| P6-B7 Preview lifetime + evidence | Bitmap clone after `Image.FromStream`; GC retention smoke; checked-in + regenerated screenshots |
| P6-B8 Stale docs | Updated gate evidence to branch tip `3d8ff73` and CI run `32785847198` |

## Schema / migrations

No new migrations in this pass (uses existing Phase 6 content schema).

## Remaining known issues

See `KNOWN_ISSUES.md`

## Phase 7

Not started.
