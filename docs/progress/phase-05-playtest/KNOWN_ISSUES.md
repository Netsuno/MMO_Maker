# Phase 5 — KNOWN ISSUES

## Accepted / honest gaps

1. Graphical WPF screenshots remain **NOT RUN** in CI (no visual capture required for this gate).
2. `FormClosed` `StopPlaytestAsync` is a **fallback** only — primary await is the WPF coordinated close gate / Quit → `Close()`.

## Risks

- In-process host tests must pass port via `PlaytestRuntimeOptions.Port` (not shared process env) to avoid races.
- Force-stop failure path is covered by an injectable test seam on `PlaytestOwnedProcessLauncher`.
