# Phase 5 — KNOWN ISSUES (at acceptance)

## Accepted / honest gaps

1. Graphical WPF screenshots remain **NOT RUN** in CI (accepted).
2. `FormClosed` `StopPlaytestAsync` is a **fallback** only — primary await is the WPF coordinated close gate / Quit → `Close()`.

## Risks / debt carried forward

- In-process host tests must pass port via `PlaytestRuntimeOptions.Port` (not shared process env) to avoid races.
- Force-stop failure path is covered by an injectable test seam on `PlaytestOwnedProcessLauncher`.
- MariaDB remains on the game-server path as temporary legacy; Phase 6 must not add new MariaDB features.
