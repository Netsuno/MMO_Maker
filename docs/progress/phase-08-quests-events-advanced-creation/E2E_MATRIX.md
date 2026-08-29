# Phase 8 — E2E_MATRIX

Headless PostgreSQL-backed network scenario (mandatory gate). Status updated as steps are implemented.

| Step | Description | Test | Status |
| ---: | --- | --- | --- |
| 1 | Publish map event (multi-page, conditions) | | NOT RUN |
| 2 | Publish dialogue, quest, profession, recipe, environment deps | | NOT RUN |
| 3 | Leave draft content unpublished | | NOT RUN |
| 4 | Start built server with PostgreSQL | | NOT RUN |
| 5 | Auth + character select via network | | NOT RUN |
| 6 | Enter map — published content only | | NOT RUN |
| 7 | Action trigger (range/direction resolution) | | NOT RUN |
| 8 | Typed dialogue state | | NOT RUN |
| 9 | Reject invalid/replayed dialogue choice | | NOT RUN |
| 10 | Valid choice → quest start | | NOT RUN |
| 11 | Page change after condition change | | NOT RUN |
| 12 | Objectives via public gameplay paths | | NOT RUN |
| 13 | Disconnect/reconnect quest progress | | NOT RUN |
| 14 | Craft recipe — atomic state | | NOT RUN |
| 15 | Retry craft — no duplication | | NOT RUN |
| 16 | Quest completion — reward once | | NOT RUN |
| 17 | Retry completion — no duplicate reward | | NOT RUN |
| 18 | Contact, autorun, parallel triggers | | NOT RUN |
| 19 | Region boundary — weather/lighting | | NOT RUN |
| 20 | Server stop/restart | | NOT RUN |
| 21 | Reconnect — persistence | | NOT RUN |
| 22 | Republish + refresh workflow | | NOT RUN |
| 23 | Clean shutdown | | NOT RUN |

## Multi-client matrix

| Scenario | Status |
| --- | --- |
| Per-character switch/quest isolation | NOT RUN |
| Dialogue token single-character | NOT RUN |
| One-time event reward concurrency | NOT RUN |
| Simultaneous quest completion | NOT RUN |
| Concurrent craft + duplicate request IDs | NOT RUN |
| Parallel/autorun without duplicate runners | NOT RUN |
| Consistent map weather/lighting | NOT RUN |
| Stale connection displacement | NOT RUN |

## Phase 9

Not started.
