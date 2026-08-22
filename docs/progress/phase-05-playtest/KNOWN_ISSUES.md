# Phase 5 — KNOWN ISSUES / LIMITATIONS

## Known limitations

1. **Playtest world transport** is manifest + `.fmap` files written by the editor preparer. The server does not open PostgreSQL during playtest (by design: no DB string on server/client).
2. **Runtime map IDs** are session-allocated ints (primary=1). Warp Guids in blobs are rewritten to packed runtime Guids for existing `MapService` warp resolution.
3. **Client auto-login** is not forced; playtest opens the client with host/port prefilled — operator still clicks Connect/Login (demo/demo) unless future UX automates it.
4. **Windows smoke ×3** and real WPF screenshots are validated on CI Windows runners; this Linux agent provides schematics + non-UI E2E.
5. **MariaDB** playtest path is intentionally unused (Phase 5 = PostgreSQL published maps only).

## Three principal remaining risks

1. **Process tree kill portability** — `Kill(entireProcessTree: true)` behavior can differ across Windows shells if `dotnet Frog.Server.dll` is used; orphan risk if OS denies kill.
2. **Ephemeral port races** — rare bind race between free-port probe and server listen under heavy CI load.
3. **Warp closure completeness** — only **published** warp targets are included; unpublished destination maps fail playtest preparation (correct, but authors must publish destinations first).

## Phase 6

Not started.
