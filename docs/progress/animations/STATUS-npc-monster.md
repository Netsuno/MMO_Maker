# STATUS — NPC / monster walk animation MVP

| Champ | Valeur |
| --- | --- |
| **Chantier** | Idle + 4-dir walk for NPCs and monsters (parallel to combat, after player #25) |
| **Propriétaire** | Netsun |
| **Statut** | Draft MVP branché sur `MapViewRenderer` / `WorldEntityAssets` — **pas de merge** |
| **Base** | `main` @ `d6e5975` (re-pin after #33) |
| **Branche** | `cursor/npc-monster-walk-anim-4237` |
| **PR** | Draft (this branch) vers `main` — **pas de merge** |
| **Tip** | *(pin after first push)* |
| **CI** | pending |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** — aucun champ fil facing / anim |

`WorldMetrics.DefaultTileSizePixels = 32` inchangé. Affichage nearest-neighbor ×1.
Pas de bump protocole. Combat, auction, instances, maintenance, weather **non touchés**.
Aucun edit de panneau Phase 8 **exact-sha** (`EnvironmentPanel` / `02`–`04`).

## Ce qui est livré

1. **Horloge partagée** — `WalkClock` (cycle 0→1→2→1, 140 ms). `PlayerWalkClock` délègue. `WorldSpritePose` + `WorldEntityKind` (Npc / Monster). **Aucun champ fil.**
2. **Frames CC0 in-repo** — `tools/generate-npc-monster-sprites.py` :
   - `npc-walk.png` (96×128) : Eldiran row 9 (villageois brun), même layout que le joueur. `npc.png` = idle sud.
   - `monster-walk.png` (96×128) : slime procédural original (stdlib zlib). `monster.png` = idle sud.
   Aucun téléchargement, aucune sheet Graal, aucun pack externe.
3. **Draw path** — `MapViewRenderer.Render` accepte `npcCentersPx` / `npcPoses` / `monsterCentersPx` / `monsterPoses` → `WorldEntityAssets.DrawFeetAnchored` / `FrameFor`. Dessin après prefabs / events, avant joueurs ; overlay météo toujours **dernier**.
4. **Client** — `MainShellForm.RedrawMap` passe les collections. `AdvanceWorldEntitySmoothing` avance le walk-clock + facing depuis le vecteur visuel (même pattern que les autres joueurs). Pas de hook combat.

## Hors scope (volontaire)

- IA monstre / route serveur / spawn combat.
- Bump `FrogWireProtocol.Version`, facing on the wire.
- Auction, instances, maintenance, weather.
- Panneaux Phase 8 exact-sha.
- Equipment overlays / résolution monde &gt; 32×32.

## Tests

- Linux : `Frog.Tests/ClientNpcMonsterAnimTests.cs` (IHDR 96×128, draw path, WalkClock, protocole 11, ce STATUS) + `PlayerWalkClockTests.WalkClock_MatchesPlayerWalkClock`.
- Windows smoke : `MapViewRendererSmokeTests.NpcAndMonsterWalkPose_DrawFeetAnchored_StayThirtyTwoAndNotGold`.

Linux / cet agent : pas de capture WinForms live. Revue pixel = strips générés + smokes Windows CI.
