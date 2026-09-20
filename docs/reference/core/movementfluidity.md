# MovementFluidity (#29)

← [Core](README.md) · Tip : `d6e59759` · merged #29 · **Référence only** (pas de guide joueur)

Helpers client-only sur le chemin predict / camera / other-player. Protocole / collision / walk-sheet **inchangés**.

## Baseline before (Win `2faa511`) — seuls chiffres publiés

| Lane | mean | max |
| --- | --- | --- |
| `render_intent_to_visible_ms` | 20.3 | 40.4 |
| `net_send_to_local_correction_ms` | 2.9 | 181.1 |
| `frame_dt_ms` | 25.2 | 46.7 |

**Aucun chiffre *after* inventé.** Re-mesure Windows requise.

## Méthodes clés

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| sanitizerawdt | `sanitizerawdt(dtSeconds)` | ≤0 ou >0.25 → 1/60 |
| clampvisualdt | `clampvisualdt(dtSeconds)` | Cap **48 ms** visual |
| expalpha | `expalpha(convergencePerSec, dt)` | α expo |
| resolvelocalserversample | `resolvelocalserversample(vis, prev, incoming, mapChanged)` | Rejette ack stale |
| steptoward | `steptoward(…, alpha, maxStepPx)` | Lerp + cap pas |
| predictspeed / othermaxsteppixels | helpers | Vitesse / cap other |

Constantes notables : `MaxVisualDtSeconds=0.048`, `CameraConvergencePerSec=16`, `OtherConvergencePerSec=14`, `SnapDesyncPx=256`.
