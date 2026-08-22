# Phase 5 — KNOWN ISSUES / LIMITATIONS

## Evidence honesty

- `playtest-launch.png` / `playtest-client-running.png` are **schematic mockups**, not validated WPF screenshots.
- Manual graphical desktop test: **NOT RUN** (Linux cloud agent).
- Production client GUI path on Windows is covered by smoke + `FrogGameClient` protocol tests; Linux CI uses `PlaytestOwnedProcessLauncher` + production-equivalent headless client that speaks the same token/READY protocol as `Frog.Client`.

## Known limitations

1. Playtest world transport = owned workspace under `%TEMP%/frog-playtest/{correlation}` + `.fmap` blobs (no PostgreSQL on child processes).
2. Ephemeral playtest auth uses a single-use loopback token (`__frog_playtest__` + token); never logged.
3. `FormClosed` `StopPlaytestAsync` is a **fallback** only — primary await is the WPF coordinated close gate / Quit → `Close()`.
4. MariaDB playtest path unused.

## Three principal remaining risks

1. Process-tree kill portability for `dotnet Frog.Server.dll` / WinExe client under some Windows shells.
2. Ephemeral port race under heavy CI load.
3. Headless Linux client helper mirrors protocol READY marker but is not the WinForms binary (Windows smoke exercises real `Frog.Client` handshake for protocol-version rejection; full GUI auto-playtest path runs when Windows smoke launches real client exes in production launcher scenarios).

## Phase 6

Not started.
