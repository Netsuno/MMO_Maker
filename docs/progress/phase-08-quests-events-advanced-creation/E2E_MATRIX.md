# Phase 8 — E2E_MATRIX

Headless PostgreSQL-backed network scenario (mandatory gate). Status updated as steps are implemented.

Evidence pin: implementation tip `09e68dfcb86d0b479515d70b13f1bf607afa7926`, CI https://github.com/Netsuno/MMO_Maker/actions/runs/35274081277 (green). Prior pin `ebc96921` / CI 34436843321. 23-step + multi-client PASS on the current run. P1 public TCP Interact identity: `FrogWireProtocol.Version = 10`, `Phase8InteractIdentityTcpTests` ×6. Capture/manifest tip remains `fa8c44f` (separate from the implementation tip).

| Step | Description | Test | Status |
| ---: | --- | --- | --- |
| 1 | Publish map event (multi-page, conditions) | `Phase8PostgresE2ETests` | PASS |
| 2 | Publish dialogue, quest, profession, recipe, environment deps | `Phase8PostgresE2ETests` | PASS |
| 3 | Leave draft content unpublished | `Phase8PostgresE2ETests` | PASS |
| 4 | Start built server with PostgreSQL | `Phase8PostgresE2ETests` | PASS |
| 5 | Auth + character select via network | `Phase8PostgresE2ETests` | PASS |
| 6 | Enter map — published content only | `Phase8PostgresE2ETests` | PASS |
| 7 | Action trigger (range/direction resolution) | `Phase8PostgresE2ETests` | PASS |
| 8 | Typed dialogue state | `Phase8PostgresE2ETests` | PASS |
| 9 | Reject invalid/replayed dialogue choice | `Phase8PostgresE2ETests` | PASS |
| 10 | Valid choice → quest start | `Phase8PostgresE2ETests` | PASS |
| 11 | Page change after condition change | `Phase8PostgresE2ETests` | PASS |
| 12 | Objectives via public gameplay (Talk/Visit/Collect/Kill/Craft) + counter asserts + per-kind replay | `Phase8PostgresE2ETests` | PASS |
| 13 | Disconnect/reconnect quest progress (mid-progress + completed) | `Phase8PostgresE2ETests` | PASS |
| 14 | Craft recipe — atomic state | `Phase8PostgresE2ETests` | PASS |
| 15 | Retry craft — no duplication | `Phase8PostgresE2ETests` | PASS |
| 16 | Quest completion — reward once | `Phase8PostgresE2ETests` | PASS |
| 17 | Retry completion — no duplicate reward | `Phase8PostgresE2ETests` | PASS |
| 18 | Contact, autorun, parallel triggers | `Phase8PostgresE2ETests` | PASS |
| 19 | Region boundary — weather/lighting | `Phase8PostgresE2ETests` | PASS |
| 20 | Server stop/restart | `Phase8PostgresE2ETests` | PASS |
| 21 | Reconnect — persistence | `Phase8PostgresE2ETests` | PASS |
| 22 | Republish + live refresh of already-connected session (catalog / map events / environment push + new dialogue, no reconnect) | `Phase8PostgresE2ETests` | PASS |
| 23 | Clean shutdown | `Phase8PostgresE2ETests` | PASS |

## Multi-client matrix

| Scenario | Status |
| --- | --- |
| Per-character switch/quest isolation | PASS (`PerCharacterSwitchAndQuest_IsolatedBetweenClients`) |
| Dialogue token single-character | PASS (`DialogueToken_SingleCharacterOnly`) |
| One-time event reward concurrency | PASS (`MapEventOnceRewardRace_SameCharacter_ExactlyOneItem`) |
| Simultaneous quest completion | PASS (`QuestTurnInRace_SameCharacter_ExactlyOneReward`) |
| Concurrent craft + duplicate request IDs | PASS (`CraftConcurrentRetry_SameClient_NoDuplication`) |
| Parallel/autorun without duplicate runners | PASS (`AutorunMapEvent_DoesNotDuplicateOnSecondLogin`) |
| Consistent map weather/lighting | PASS (`EnvironmentState_ConsistentAcrossClientsOnSameMap`) |
| Stale connection displacement | PASS (`Reconnect_DisplacesStaleConnection`) |

## Phase 9

Not started.
