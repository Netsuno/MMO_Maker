# STATUS — Movement fluidity MVP

| Field | Value |
| --- | --- |
| **Work** | Cheap feel wins on the existing predict / camera / smoothing path (priority #1 after baseline) |
| **Owner** | Netsun |
| **Status** | Draft MVP — do not merge |
| **Base** | `main` @ `bc60186` (merge PR #26 prefabs; includes #25 walk anim + #24 MEASURE-BASELINE fill) |
| **Branch** | `cursor/movement-fluidity-mvp` |
| **PR** | Draft toward `main` — **do not merge** |
| **Tip** | _(pinned after push)_ |
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

---

## Hors scope (unchanged)

- Protocol / `PositionSync` payload
- `MovementService.TryApplyMove` / `TryApplyReportedPixelPosition`
- Collision radius / blocked tiles
- `PlayerWalkClock` / walk sheets / facing-on-wire
- Prefabs, social panels, sound
