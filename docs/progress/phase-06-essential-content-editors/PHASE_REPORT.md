# Phase 6 — Essential Content Editors (Second Fix Pass)

## Status

**GATE REACHED — WAITING FOR REVIEW**

## Implementation SHA

`1a09213` — code under review for P6-B1…B7 fixes (subsequent commits may be documentation-only)

## Second-pass blocker fixes

| Blocker | Fix |
| --- | --- |
| P6-B1 Initial category blank | `ShowInitialCategory()` after `_initialized = true`; smoke asserts tileset panel visible without manual category change |
| P6-B2 DbContext leak smoke ineffective | `PostgresGameDataScopeLifecycleTests` on real PostgreSQL (migrate once, dispose, drain, repeated open/close) |
| P6-B3 Shared DbContext concurrency | `FrogDbContextGate` serializes EF operations; `GameDataPanelAsyncGate` serializes panel UI async; form close drains panels + scope |
| P6-B4 “Toutes…” spawn filters | `NormalizeFilterId` converts `Guid.Empty` → `null`; spawn filter UI smoke |
| P6-B5 Dirty navigation desync | `GameDataListNavigation` helper on all panels (revert list selection, preserve dirty edits) |
| P6-B6 UI smoke coverage | Full matrix per editor: dup/delete, invalid publish, search/filter, close/reopen, cancel nav, protected delete |
| P6-B7 Preview lifetime + evidence | Bitmap clone after `Image.FromStream`; GC retention smoke; checked-in + regenerated screenshots |
| P6-B8 Stale docs | This update; distinguish implementation SHA from branch tip |

## Schema / migrations

No new migrations in this pass (uses existing Phase 6 content schema).

## Remaining known issues

See `KNOWN_ISSUES.md`

## Phase 7

Not started.
