# STATUS — Client UI modernization

| Champ | Valeur |
| --- | --- |
| **Chantier** | Client UI modernization (présentation Frog.Client) |
| **Propriétaire** | Netsun |
| **Statut** | **E0 (+ E1 partiel) IN PROGRESS** — thème DA appliqué ; layout inchangé |
| **Base** | `main` @ `e58a18580dff4fa72b513288fb57fcf1c22c9d12` (merge PR #8 Phase 10) |
| **Inventaire UI** | Code live = tip `e58a185` (ex-P10) |
| **Branche** | `cursor/client-ui-modernization` |
| **PR** | Draft https://github.com/Netsuno/MMO_Maker/pull/9 vers `main` — **pas de merge** |
| **Non-interférence** | Ne pas toucher `cursor/phase10-beta-release` / PR #8 ; pas de wire / TLS |

Protocole inchangé (v11, tip `e58a185`). Pas de nouvel opcode.

---

## Ce run (livré)

- [x] Analyse UI actuelle (`MainShellForm` + panneaux live + stubs historiques)
- [x] Inventaire : fonctionne / se conserve / présentation-only
- [x] Architecture overlay + thème sombre/or + services existants
- [x] Découpe en petites étapes indépendantes + critères done + risques 60 FPS
- [x] Docs dans `docs/progress/client-ui-modernization/`
- [x] Recalage inventaire P10 `853776e` (Options/Help/Trade/settings live ; stubs folklore ; mapping extraits)
- [x] Tokens DA + couches `GameWorldView` ⊥ `HudLayer` ⊥ `WindowLayer` ⊥ `LoginShell` ; plan **E0–E8** ([TOKENS-DA.md](TOKENS-DA.md))
- [x] Sync branche sur `main` `e58a185`
- [x] **E0** : `UiTheme` + chrome sombre/or (Apply-once). Surfaces SHA Phase 8 (`DialoguePanel` / `QuestJournalPanel` / `EnvironmentPanel`) **non** recolorées.
- [x] **E1 partiel** : login/perso/options/aide/trade thématisés ; hôte/port restent sur login **et** Options → Réseau (`LastHost`/`LastPort` dans le JSON existant). Pas de bouton factice.

## Pas livré (volontaire)

- [x] Thème sombre/or appliqué (E0, layout inchangé)
- [ ] Host overlay / carte plein cadre (E2)
- [ ] HUD joueur / minimap / hotbar (E3–E5)
- [ ] Fenêtres inventaire / perso / quêtes / shop / dialogue restylées (E6 ; SHA Phase 8 02–04 encore exactes)
- [x] Restyle `OptionsForm` / `HelpForm` / `TradeForm` (store unique + Réseau)
- [ ] Isolation perf du `MapViewRenderer` (full-map `Bitmap` à chaque dirty tick)

---

## Relation aux autres chantiers

| Chantier | Relation |
| --- | --- |
| Phase 7–8 (gameplay / quêtes) | **ACCEPTED on main** — fonctionnalités à **conserver** ; ce chantier ne change que la présentation |
| Phase 9 (distribution / admin) | Merge `f74b34c` sur `main` — hors scope UI |
| Phase 10 / PR #8 | **MERGED** `e58a185`. Cette branche est rebasée dessus. |
| Trade / Party / Guild | Présents sur `853776e` (UI + enum). Cette PR docs n’ajoute aucun opcode. |

`docs/STATUS.md` (racine) reste le statut Phase 9. Ce fichier est le statut **uniquement** du chantier UI client.

---

## Prochaine action (run suivant, pas celui-ci)

**E2** : carte plein cadre + HUD vides — **pas** dans ce commit (risque smokes + fluidité). Mouvement / `RedrawMap` inchangés. Linux : compile targeting Windows, pas de GUI WinForms ici.
