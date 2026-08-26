# Phase 7 — Known Issues

## Remediations landed (P7-FIX-1…5)

Prior rejection items were addressed in code. Remaining limitations:

1. Unused legacy stub files under `Frog.Client/Models/*` and some `Frog.Client/Services/*` still contain `// TODO` placeholders; gameplay uses `MainShellForm` + `FrogGameClient` + inventory/equipment panels instead.
2. Legacy MariaDB adapters remain for older map/player world-state paths; Phase 7 **auth/gameplay production** requires PostgreSQL and does not select MariaDB identity.
3. Gameplay-client screenshot SHA-256 hashes in `SCREENSHOT_MANIFEST.md` are filled from the CI artifact after Windows smoke ×3 (placeholders until CI uploads).
4. Linux agents report Windows gameplay/editor smokes as **NOT RUN** (STA WinForms); CI Windows job is authoritative.

## Phase 8

Not started.
