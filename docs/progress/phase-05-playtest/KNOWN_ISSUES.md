# Phase 5 — KNOWN ISSUES / LIMITATIONS

## Evidence honesty

- `playtest-launch.png` and `playtest-client-running.png` are **schematic mockups**, not validated WPF screenshots.
- Manual visual test of the running editor/client UI: **NOT RUN** (no Windows graphical session on the Linux cloud agent). Automated proof of real process launch/stop remains in `PlaytestRealProcessLifecycleTests` and CI Windows smoke.

## Known limitations

1. **Playtest world transport** is manifest + `.fmap` files written by the editor preparer. The server does not open PostgreSQL during playtest (by design: no DB string on server/client after sanitize).
2. **Runtime map IDs** are session-allocated ints (primary=1; others by stable Guid order). Warp Guids in blobs are rewritten to packed runtime Guids for `MapService` warp resolution.
3. **Client auto-login** is not forced; playtest opens the client with host/port/correlation — operator still authenticates unless future UX automates it.
4. **MariaDB** playtest path is intentionally unused (Phase 5 = PostgreSQL published maps only).

## Three principal remaining risks

1. **Process tree kill portability** — `Kill(entireProcessTree: true)` can differ across Windows shells when launching `dotnet Frog.Server.dll`; residual orphan risk if the OS denies kill.
2. **Ephemeral port races** — rare bind race between free-port probe and server listen under heavy CI load.
3. **Warp closure completeness** — only **published** warp targets (direct and transitive) are included; unpublished destinations fail preparation clearly (authors must publish destinations first).

## Phase 6

Not started.
