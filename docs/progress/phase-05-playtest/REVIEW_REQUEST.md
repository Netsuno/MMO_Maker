# Demande de revue — Phase 5 (fifth rejection corrections)

## Contexte

Fifth temporary rejection at `b6991aa695da5b14690bd46696c533692dea56ce`. Corrections on **same** branch/PR only. Phase 6 not started.

## Plage

- After: `b6991aa695da5b14690bd46696c533692dea56ce`
- Implementation tip: `d33f9924e5ff4ee613475200bfb1a6dd88f0d65e`
- CI: https://github.com/Netsuno/MMO_Maker/actions/runs/32654683068

## Checklist

- [x] Reserved username detection uses shared `AccountUsername` (OrdinalIgnoreCase)
- [x] Exact / uppercase / mixed-case reserved registration rejected
- [x] Mixed-case seeded account cannot reuse token after consume
- [x] Token committed **before** successful LoginResult
- [x] Post-commit failures / abort never restore the token
- [x] Session-creation failure before commit keeps token available
- [x] Concurrent exactly-one-success preserved
- [x] Prior Phase 5 corrections preserved; screenshots NOT RUN
