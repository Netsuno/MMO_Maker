# STATUS — Client UI modernization

| Champ | Valeur |
| --- | --- |
| **Chantier** | Client UI modernization (présentation Frog.Client) |
| **Propriétaire** | Netsun |
| **Statut** | **E2–E8 IN PROGRESS / code poussé** — overlay HUD branché ; SHA Phase 8 02–04 intactes |
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
- [x] **E2** : `_worldHost` + `_mapScroll` Dock.Fill ; TabControl overlay 360×250–700 (smokes)
- [x] **E3** : Status (`CombatStateWire`) ; minimap cache `MapData` only ; quest tracker même snapshot
- [x] **E4** : `HudChatDock` — Général / Local / Whisper / Groupe / Guilde (enum live) ; ListBox cap 200
- [x] **E5** : hotbar 10 (1–3 mêlée/sort/interact ; 4–10 disabled) ; menu Perso/Inv/Quêtes/Carte/Options
- [x] **E6 partiel** : double filet or Inventaire / Équipement ; **pas** de restyle SHA 02–04
- [x] **E7** : Options nav Graphisme / Son / Contrôles / Interface / Réseau (store inchangé)
- [x] **E8 notes** : [DPI.md](DPI.md) + `AutoScaleDimensions` 96

## Restant

- [ ] Revue visuelle Windows 1366×768 / 1920×1080 / DPI 125–150 (Linux = compile only)
- [ ] Isolation perf `MapViewRenderer` full-map Bitmap (hors HUD ; pas ce commit)
- [ ] Restyle SHA Dialogue / Quêtes / Environnement **uniquement** si CI Windows régénère le manifeste
- [ ] CI verte à confirmer sur le tip poussé

---

## Relation aux autres chantiers

| Chantier | Relation |
| --- | --- |
| Phase 7–8 | ACCEPTED — fonctionnalités conservées |
| Phase 10 / PR #8 | MERGED `e58a185` |
| Trade / Party / Guild | Canaux réels dans le dock — pas d’opcode ajouté |
