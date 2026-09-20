# STATUS — Player / NPC animation MVP

| Champ | Valeur |
| --- | --- |
| **Chantier** | Idle + 4-dir walk on the layered 32×32 player skin (body/head) |
| **Propriétaire** | Netsun |
| **Statut** | MVP branché sur `MapViewRenderer` / GameWorldView — **pas de merge** |
| **Base** | `main` @ `2faa511` (merge PR #23 DA v2 login) |
| **Branche** | `cursor/player-npc-anim-mvp` |
| **PR** | Draft vers `main` — **pas de merge** |
| **Tip** | _(pin after first green CI)_ |

`WorldMetrics.DefaultTileSizePixels = 32` inchangé. Affichage nearest-neighbor ×1.
Pas de bump protocole, pas de rewrite caméra / fluidité, pas de prefabs, pas d’IA monstre.

## Ce qui est livré

1. **Frames CC0 in-repo** — `tools/generate-player-sprite.py` extrait le bloc Eldiran déjà vendorié (`cols 0–2 × rows 4–7`) : idle planté (colonne 1) + 2 pas par direction. Aucun téléchargement, aucune sheet Graal.
2. **Couches** — `player-walk-body.png` + `player-walk-head.png` (96×128). Draw order inchangé : **body → tunic → armor → head → weapon**. Tunique / armure / arme restent vides.
3. **Compat** — `player.png` / `player-body.png` / `player-head.png` = idle sud (secours).
4. **Horloge** — `PlayerWalkClock` (cycle 0→1→2→1, 140 ms). Facing local = vecteur des touches tenues. Autres joueurs = vecteur d’interpolation visuelle. **Aucun champ fil.**
5. **Draw path** — `MainShellForm.RedrawMap` passe `PlayerSpritePose` à `MapViewRenderer.Render` → `PlayerWorldAssets.DrawFeetAnchored` / `FrameFor`.

Remote players reuse the same sprite path (the only “NPC” draw on GameWorldView today). Monster / event-NPC AI anim is out of scope.

## Hors scope

- Full monster AI anim
- Fluidity / prediction rewrite
- Camera rewrite
- Protocol bump / facing on the wire
- Prefabs / equipment overlays
- Raising world resolution above 32×32

## Tests

- Linux : `Frog.Tests/PlayerWalkClockTests.cs` + `ClientPlayerAnimTests.cs` (IHDR 96×128, compose/draw path, STATUS).
- Windows smoke : `MapViewRendererSmokeTests.WalkPose_ComposesBodyAndHead_FourDirsStayThirtyTwo` — composition + pose walk toujours bleu, pas ellipse or.

Linux / cet agent : pas de capture WinForms live. Revue pixel = strip généré + smokes Windows CI.
