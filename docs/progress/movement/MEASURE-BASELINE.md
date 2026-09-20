# MEASURE-BASELINE — Tile movement (Netsun priority #1 prep)

| Field | Value |
| --- | --- |
| **Work** | Measurement only — baseline tile / pixel movement before any fluidity work |
| **Owner** | Netsun |
| **Status** | Instrumentation shipped, **off by default**. Numbers below are placeholders until Windows capture |
| **Base** | `main` @ `60757c9` (merge PR #16 DA v2 contrast) |
| **Branch** | `cursor/movement-measure-baseline-4576` |
| **PR** | Draft [#18](https://github.com/Netsuno/MMO_Maker/pull/18) toward `main` — **do not merge as a movement fix** |
| **Tip** | `2f41ebb89646d5f312d3bce0c9c34895b7a593da` |
| **CI** | [35515532160](https://github.com/Netsuno/MMO_Maker/actions/runs/35515532160) **SUCCESS** (`build-and-test` + `postgres-integration`) |
| **Out of scope** | No prediction rewrite, no collision redesign, no camera overhaul, no animation system |

This document is how **Orchestrator / Netsun** run the baseline on **Windows**. Linux CI can compile and unit-test the probe; it cannot capture WinForms key → sprite latency.

---

## What is measured (existing hooks only)

| Lane | Hook (already in the play path) | Metric |
| --- | --- | --- |
| **Input** | `MainShell_KeyDown` new move-key edge → `RecomputeHeldMoveKeys` `becameHeld` | `input_press_to_intent_ms` |
| **Render** | First `RedrawMap` + `ApplyMapViewportCamera` after that intent | `render_intent_to_visible_ms` |
| **Network (local)** | `TrySendHeldMoveNetwork` `PositionSync` send → local `PositionUpdate` | `net_send_to_local_correction_ms` |
| **Network (other)** | Interval between other-player `PositionUpdate` applies | `net_other_player_update_interval_ms` |
| **Network (server)** | `PacketDispatcher` apply elapsed when flag is on (throttled `movement_measure apply`) | `server apply_ms` — existing `MoveApplied` EventId **5016** remains Debug |
| **FPS** | `AdvanceMovementSmoothing` `dt` vs `_smoothTimer` 16 ms | `frame_dt_ms` — predict step is already **dt-scaled** (`speedPxPerSec * dt`) |

Unchanged constants to record next to samples:

| Constant | Value | Where |
| --- | --- | --- |
| Smooth timer | **16 ms** | `MainShellForm._smoothTimer` |
| Network pulse | **52 ms** | `MainShellForm.MoveNetworkPulseMs` |
| Step | **8 px** / request | `WorldMetrics.PlayerMovePixelsPerRequest` |
| Tile | **32 px** | `WorldMetrics.DefaultTileSizePixels` |

`press→intent` is often **~0 ms** (same KeyDown stack). That is a valid baseline: intent is not queued across frames at the input layer. The useful first-paint number is `intent→visible`.

---

## How to run on Windows (Orchestrator / Netsun)

Flag: `FROG_MOVEMENT_MEASURE=1` (also `true` / `yes` / `on`). **Unset = off.** DEBUG builds stay silent without the flag. Release builds never log measure lines unless the flag is set.

### 1. Server (Terminal A)

```powershell
cd <repo>
dotnet run --project Frog.Server/Frog.Server.csproj
```

Optional server-apply lines (throttled Information `movement_measure apply`):

```powershell
$env:FROG_MOVEMENT_MEASURE = "1"
dotnet run --project Frog.Server/Frog.Server.csproj
```

Without the env var the server only keeps the existing Debug `Move applied` (EventId 5016). Default `PacketDispatcher` log level is Debug in `Frog.Server/appsettings.json`.

### 2. Client (Terminal B) — required for input / render / local net

```powershell
cd <repo>
$env:FROG_MOVEMENT_MEASURE = "1"
dotnet run --project Frog.Client/Frog.Client.csproj
```

1. Connect `127.0.0.1:6000`, login, enter the world.
2. Confirm the log shows `[measure] FROG_MOVEMENT_MEASURE=1 — baseline on`.
3. Tap **one** direction key (ZQSD / WASD / arrows), wait until the sprite stops, repeat **10+** times.
4. Hold a direction for **~3 s** (network pulse samples).
5. **Copier diagnostics** — the report appends a `[measure] summary …` line when the flag is on.
6. Paste log lines + summary into the tables below.

### 3. Other-player lane (optional, two clients)

Terminal C: second client, **same** `FROG_MOVEMENT_MEASURE=1`, second account. Walk with client B while watching client C (or the reverse). Record `net other-player interval`.

### 4. Confirm default-off (sanity)

```powershell
Remove-Item Env:FROG_MOVEMENT_MEASURE -ErrorAction SilentlyContinue
dotnet run --project Frog.Client/Frog.Client.csproj
```

No `[measure]` banner, no measure lines in the client log, diagnostics have no summary line.

---

## What to record

Copy last / min / mean / max from `[measure] summary` (or the one-shot lines). Leave `_` until a Windows session fills them.

### Session metadata

| Item | Value |
| --- | --- |
| Date (UTC) | _ |
| Machine / GPU | _ |
| Windows build / DPI | _ |
| Client config (Debug / Release) | _ |
| Tip SHA | _ |
| Map / spawn | _ |
| Localhost or remote host | _ |
| Second client used? | _ |

### Input

| Sample | last | min | mean | max | n | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `input_press_to_intent_ms` | _ | _ | _ | _ | _ | expect ~0 if same KeyDown stack |

### Render (first visible sprite + camera after intent)

| Sample | last | min | mean | max | n | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `render_intent_to_visible_ms` | _ | _ | _ | _ | _ | vs 16 ms timer |

### Network

| Sample | last | min | mean | max | n | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `net_send_to_local_correction_ms` | _ | _ | _ | _ | _ | last send → next local `PositionUpdate` |
| `net_other_player_update_interval_ms` | _ | _ | _ | _ | _ | two clients; n=0 if solo |
| `server apply_ms` (throttled) | _ | _ | _ | _ | _ | from server console if flag on |

### FPS independence

| Sample | last | min | mean | max | n | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `frame_dt_ms` | _ | _ | _ | _ | _ | compare to 16 ms; clamp in code if `dt` ≤0 or >250 ms → 1/60 s |
| Visible stutter vs frame time? | _ | | | | | qualitative: none / mild / obvious |
| Predict step dt-scaled? | yes (`speedPxPerSec * dt`) | | | | | do **not** change this in a follow-up “fix” without a new mandate |

---

## Log line cheat-sheet

Client (`_txtLog` + Debug output), only when the flag is on:

```
[measure] FROG_MOVEMENT_MEASURE=1 — baseline on (docs/progress/movement/MEASURE-BASELINE.md)
[measure] input press→intent 0.1ms
[measure] render intent→visible 16.4ms
[measure] net send→local 24.2ms
[measure] net other-player interval 52.0ms
[measure] summary input_press_to_intent_ms: n=… | render_intent_to_visible_ms: n=… | …
```

Network one-shots: first 4, then every 10th. Summary: every ~2 s or 12 events.

Server (Information, throttled: first 8 then every 20th):

```
movement_measure apply username=demo pixel=(x,y) apply_ms=0.4
```

---

## What this PR did **not** change

- `TryApplyLocalPredictStep` / convergence / snap-desync
- `MovementService.TryApplyMove` / `TryApplyReportedPixelPosition` commit rules
- `MapViewportCamera` math
- Protocol, pulse 52 ms, step 8 px

Next fluidity work should start from the filled tables, not from a rewrite.
