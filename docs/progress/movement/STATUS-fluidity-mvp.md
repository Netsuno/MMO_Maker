# STATUS — Movement fluidity MVP

| Champ | Valeur |
| --- | --- |
| **Chantier** | Cheap feel wins sur le chemin predict / camera / smoothing existant |
| **Propriétaire** | Netsun |
| **Statut** | **Merged** sur `main` — client-only ; **pas** de page guide joueur |
| **Merge** | [PR #29](https://github.com/Netsuno/MMO_Maker/pull/29) → `8a51f5cc36df2aebc3fd2680d36a307153484585` |
| **Tip miroir docs** | `d6e59759dada9ef24849b9b985838b459c7f55b1` (main tip courant) |
| **Tip feature (CI green)** | `968cd77ee3f7c748151551ada8dc8abe7e4972ca` |
| **CI (feature tip)** | [35536775088](https://github.com/Netsuno/MMO_Maker/actions/runs/35536775088) **SUCCESS** |
| **Baseline** | Windows session tip `2faa511` — nombres **before-only** dans [MEASURE-BASELINE.md](https://github.com/Netsuno/MMO_Maker/blob/main/docs/progress/movement/MEASURE-BASELINE.md) (dépôt) |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** |

Docs : **Référence-Core seulement** (pas de `PLAYER_QUICKSTART` / pas de wiki Joueur « fluidité »). Aucune métrique *after* inventée.

---

## Baseline (before) — Win session tip `2faa511`

| Lane | last | min | mean | max | n | Feel |
| --- | --- | --- | --- | --- | --- | --- |
| `input_press_to_intent_ms` | 0.0 | 0.0 | 0.0 | 0.4 | 33 | same KeyDown stack |
| `render_intent_to_visible_ms` | 3.3 | 3.3 | **20.3** | **40.4** | 33 | first paint waited on the 16 ms timer |
| `net_send_to_local_correction_ms` | 1.0 | 0.0 | **2.9** | **181.1** | 87 | mean fine ; spike = hitch |
| `frame_dt_ms` | 18.3 | 1.8 | **25.2** | 46.7 | 1158 | vs 16 ms timer |

Constantes inchangées : timer **16 ms**, pulse **52 ms**, step **8 px**, tile **32 px**, protocole **11**.

---

## After (merged) — intent, **pas** une nouvelle session Windows

Trois helpers client + câblage `MainShellForm` :

| Symptôme (baseline) | Cheap win | Où |
| --- | --- | --- |
| intent→visible mean 20.3 / max 40.4 | Sur `becameHeld`, paint immédiat avant le tick timer | `RecomputeHeldMoveKeys` |
| frame_dt mean 25.2 + stall 181 ms | Cap **visual** dt à **48 ms** (`ClampVisualDt`) ; mesure raw inchangée | `MovementFluidity` |
| net local max 181 ms | Rejette ack local plus loin du sprite que l’échantillon précédent | `ResolveLocalServerSample` |
| hitch camera | `DampFocus` 16/s ; snap first paint / map / 256 px | `MapViewportCamera.DampFocus` |

### Expected measure movement — **non mesuré après merge**

Linux / cet agent **ne peut pas** recapturer WinForms. Aucun chiffre *after* n’est publié ici. Re-run `FROG_MOVEMENT_MEASURE=1` sur Windows pour remplir after-numbers (voir MEASURE-BASELINE dépôt).

| Lane | Intent (non chiffré) |
| --- | --- |
| `render_intent_to_visible_ms` | mean devrait baisser (paint same-stack) |
| `net_send_to_local_correction_ms` | *timing* paquet inchangé ; sample *appliqué* moins yank derrière le sprite |
| `frame_dt_ms` | toujours jitter réel ; step visible plafonné |
| `input_press_to_intent_ms` | toujours ~0 |

---

## Hors scope

- Protocol / `PositionSync`, collision, `PlayerWalkClock`, rewrite serveur move.
- Page guide joueur / captures fluidité.

---

## Référence docs

- [movementfluidity](../../reference/core/movementfluidity.md) · [mapviewportcamera-dampfocus](../../reference/core/mapviewportcamera-dampfocus.md)


## Note tests (STATUS gate — ne pas retirer)

Ces phrases sont assertées par Frog.Tests StatusDoc_* :

- **Owner** | Netsun
- `protocol 11`

**Owner** | Netsun
protocol 11
