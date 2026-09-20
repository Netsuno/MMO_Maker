# STATUS — Movement fluidity MVP

| Field | Value |
| --- | --- |
| **Work** | Cheap feel wins on the existing predict / camera / smoothing path (priority #1 after baseline) |
| **Owner** | Netsun |
| **Status** | Draft MVP — do not merge |
| **Base** | `main` after Netsun merged #27 social + #28 audio into this branch (`968cd77`) |
| **Branch** | `cursor/movement-fluidity-mvp` |
| **PR** | Draft [#29](https://github.com/Netsuno/MMO_Maker/pull/29) toward `main` — **do not merge** |
| **Tip** | `968cd77ee3f7c748151551ada8dc8abe7e4972ca` (CI green after Phase 8 live-refresh flake fix) |
| **CI** | [35536775088](https://github.com/Netsuno/MMO_Maker/actions/runs/35536775088) **SUCCESS** (`build-and-test` + `postgres-integration`) |
| **Baseline** | Windows session tip `2faa511` — numbers in [MEASURE-BASELINE.md](MEASURE-BASELINE.md) |
| **Protocol** | `FrogWireProtocol.Version` **stays 11** |

Out of scope: social panels, prefabs, sound, protocol bump, collision redesign, animation system (`PlayerWalkClock` 140 ms / 4-dir walk stays as on main).

---

## Baseline (before) — Win session tip `2faa511`

Captured `[measure] summary`:

| Lane | last | min | mean | max | n | Feel |
| --- | --- | --- | --- | --- | --- | --- |
| `input_press_to_intent_ms` | 0.0 | 0.0 | 0.0 | 0.4 | 33 | same KeyDown stack |
| `render_intent_to_visible_ms` | 3.3 | 3.3 | **20.3** | **40.4** | 33 | first paint waited on the 16 ms timer |
| `net_send_to_local_correction_ms` | 1.0 | 0.0 | **2.9** | **181.1** | 87 | mean is fine; spike is the hitch |
| `frame_dt_ms` | 18.3 | 1.8 | **25.2** | 46.7 | 1158 | vs 16 ms timer; predict already dt-scaled |

Unchanged constants: timer **16 ms**, pulse **52 ms**, step **8 px**, tile **32 px**, protocol **11**.

---

## After (this PR) — intent, not a new Windows session

No architecture rewrite. Three client-only helpers plus wiring in `MainShellForm`:

| Symptom (baseline) | Cheap win | Where |
| --- | --- | --- |
| intent→visible mean 20.3 / max 40.4 | On `becameHeld`, `AdvanceMovementSmoothing` + `RedrawMap` **before** the next timer tick (first paint in the KeyDown stack) | `RecomputeHeldMoveKeys` |
| frame_dt mean 25.2, and a 181 ms stall would step ~28 px (`speed × 0.181`) | Measure **raw** dt; cap **visual** predict / camera / other-player dt at **48 ms** (~3 timer intervals). Walk-sheet elapsed stays on raw dt. | `MovementFluidity.ClampVisualDt` |
| net local max 181 ms | Reject a local `PositionUpdate` that is farther from the sprite than the previous server sample (stale/late ack). Warps (`> 256 px`) and map changes still apply. | `MovementFluidity.ResolveLocalServerSample` |
| visible hitch / hard camera lock | Camera exponential follow (`DampFocus`, 16/s); snap on first paint, map load, and 256 px desync. Other-player lerp 17→14/s + per-tick cap (1.75× walk speed). | `MapViewportCamera.DampFocus`, `AdvanceCameraFocus` |

Predict step is still `speedPxPerSec * dt` (dt now the capped visual dt). Collision, pulse 52, step 8, walk clock, protocol: **not** redesigned.

### Expected measure movement (next Windows `FROG_MOVEMENT_MEASURE=1` session)

| Lane | Intent |
| --- | --- |
| `render_intent_to_visible_ms` | mean should drop toward the lucky 3.3 ms last sample (same-stack paint), max no longer “wait a missed 16–25 ms tick” |
| `net_send_to_local_correction_ms` | **timing** of the packet is unchanged (still measured). The *applied* server sample no longer jumps behind the sprite on a stale 181 ms ack |
| `frame_dt_ms` | still reports real timer jitter (25 ms class). Visible step no longer uses dt above 48 ms |
| `input_press_to_intent_ms` | still ~0 |

Linux / this agent cannot recapture WinForms key → sprite latency. Re-run the MEASURE-BASELINE playbook on Windows to fill after-numbers.

---

## Tests

- `Frog.Tests/MovementFluidityTests.cs` — dt cap, 181 ms predict size, stale-ack reject, other-player cap, camera damp, `ComputeDrawOffset` unchanged
- `Frog.Tests/MovementFluidityMvpTests.cs` — STATUS + wiring + protocol 11 + walk 140 ms
- Existing movement tests (`MovementMeasureBaselineTests`, `MovementPacketRateGateTests`, `MapEventMovement*`, `MapViewportCameraTests`, `PlayerWalkClockTests`) must stay green
- Linux this run: `dotnet test Frog.Tests` **631 passed** (includes the movement filter **72 passed**)
- CI **green** on `9b30b3c` : [build-and-test](https://github.com/Netsuno/MMO_Maker/actions/runs/35533232516/job/106137594306) + [postgres-integration](https://github.com/Netsuno/MMO_Maker/actions/runs/35533232516/job/106137594402). Windows editor / gameplay / Phase 8 smokes included.
- After Netsun merged `main` (#27 social) into this branch, `postgres-integration` flaked on `FullPhase8Flow` step 22: live refresh grabbed a mid-burst `MapEventsResult` still named `Phase8 Gate` (catalog 8101). Not a fluidity filter bug — the 500 ms stamp poll can fire between dialogue republish and gate rename. Helper now waits until `Republished Gate` is present.
- CI **green** on `968cd77` (includes #28 audio merge + flake fix): [build-and-test](https://github.com/Netsuno/MMO_Maker/actions/runs/35536775088/job/106147152853) + [postgres-integration](https://github.com/Netsuno/MMO_Maker/actions/runs/35536775088/job/106147152698).

---

## Hors scope (unchanged)

- Protocol / `PositionSync` payload
- `MovementService.TryApplyMove` / `TryApplyReportedPixelPosition`
- Collision radius / blocked tiles
- `PlayerWalkClock` / walk sheets / facing-on-wire
- Prefabs, social panels, sound
