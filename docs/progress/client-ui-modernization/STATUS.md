# STATUS — Client UI modernization

| Champ | Valeur |
| --- | --- |
| **Chantier** | Client UI modernization (présentation Frog.Client) |
| **Propriétaire** | Netsun |
| **Statut** | **E2–E8 + polish DA M** — tabs transitoires ; SHA Phase 8 02–04 intactes |
| **Base** | `main` @ `e58a18580dff4fa72b513288fb57fcf1c22c9d12` (merge PR #8 Phase 10) |
| **Inventaire UI** | Code live = tip `e58a185` (ex-P10) |
| **Branche** | `cursor/client-ui-modernization` |
| **PR** | Draft https://github.com/Netsuno/MMO_Maker/pull/9 vers `main` — **pas de merge** |
| **Non-interférence** | Ne pas toucher `cursor/phase10-beta-release` / PR #8 ; pas de wire / TLS |

Protocole inchangé (v11). Pas de nouvel opcode. Mouvement : `_smoothTimer` 16 ms / `RedrawMap` / `InputService.IsTextInputFocus` **non réécrits**.

---

## Livré

- [x] Docs + tokens DA + plan E0–E8
- [x] **E0** : `UiTheme` apply-once (SHA Dialogue/Quest/Environment non recolorées)
- [x] **E1 partiel** : hôte/port login **et** Options → Réseau (JSON unique)
- [x] **E2** : `_worldHost` + `_mapScroll` Dock.Fill ; TabControl **transitoire** 360×250–700 (Menu Inv/Quêtes ; Esc / Carte / clic map ferment)
- [x] **E3** : Status nom + `Lv N` + HP/MP (tooltips) ; **pas de barre XP** (max absent du fil) ; minimap cache `MapData` ; tracker
- [x] **E4** : chat owner-draw `chat.*` + préfixes `[G]/[M]/[W]/[P]/[H]` ; canaux réels ; cap 200
- [x] **E5** : hotbar 10 chiffres + tooltips (4–10 disabled) ; menu 5 pills
- [x] **E6 partiel** : double filet or Inventaire / Équipement ; **pas** de restyle SHA 02–04
- [x] **E7** : Options nav Graphisme / Son / Contrôles / Interface / Réseau (store inchangé)
- [x] **E8 notes** : [DPI.md](DPI.md) + [CAPTURES.md](CAPTURES.md) (Linux ≠ PNG)
- [x] **Polish DA M** : titlebars 18 px (Status sans titre) ; `_gameToolbar` dans l’onglet Gameplay, plus une 7e bande

## Captures Windows (Linux ne peut pas)

Ce run / CI Linux : **zéro PNG HUD**. Voir [CAPTURES.md](CAPTURES.md) + `scripts/capture-client-ui-da.ps1` (1280/1920 × DPI 100/125).

## Restant

- [ ] PNG Windows 1280×720 / 1920×1080 DPI 100 + 125 (7 cases) — **ambre DA visuelle**
- [ ] Isolation perf `MapViewRenderer` full-map Bitmap (hors HUD)
- [ ] Restyle SHA Dialogue / Quêtes / Environnement **uniquement** si CI Windows régénère le manifeste
- [ ] CI verte à confirmer (flake `PlaytestProductionLauncherTests` hors HUD durci)

---

## Relation aux autres chantiers

| Chantier | Relation |
| --- | --- |
| Phase 7–8 | ACCEPTED — fonctionnalités conservées |
| Phase 10 / PR #8 | MERGED `e58a185` |
| Trade / Party / Guild | Canaux réels dans le dock — pas d’opcode ajouté |
