# Phase 6 — Known Issues (Final targeted fix pass)

## Fixed in this pass

- Production close used `DoEvents`/`Sleep` and could dispose while work remained — replaced with async close state machine
- Close smokes waited for `IsIdle` / used force-close — replaced with real `form.Close()` during pending ops
- WPF/WinForms sync-context mismatch deferred panel ops off the click path
- Committed mockup screenshots — replaced with CI smoke artifact PNGs + SHA-256 manifest

## Remaining known issues

- Windows `GameDataInitializationLeakSmokeTests` still uses in-memory repositories (PostgreSQL lifecycle covered by integration tests)
- Shop listing / class spell protected-delete smokes use minimal published prerequisites

## Phase 7

Not started.
