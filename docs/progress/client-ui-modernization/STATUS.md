# STATUS — Client UI modernization

| Champ | Valeur |
| --- | --- |
| **Chantier** | Client UI modernization (présentation Frog.Client) |
| **Propriétaire** | Netsun |
| **Statut** | **PLAN / AUDIT ONLY** — pas de code produit HUD |
| **Base** | `main` @ `f74b34cca09dda819fe26747d48ee16d27007dfd` (merge PR #7 Phase 9) |
| **Branche** | `cursor/client-ui-modernization` |
| **PR** | Draft https://github.com/Netsuno/MMO_Maker/pull/9 vers `main` — **pas de merge** |
| **Non-interférence** | Ne pas toucher `cursor/phase10-beta-release` / PR #8 |

Protocole inchangé : `FrogWireProtocol.Version = 10`.

---

## Ce run (livré)

- [x] Analyse UI actuelle (`MainShellForm` + panneaux live + stubs historiques)
- [x] Inventaire : fonctionne / se conserve / présentation-only
- [x] Architecture overlay + thème sombre/or + services existants
- [x] Découpe en petites étapes indépendantes + critères done + risques 60 FPS
- [x] Docs dans `docs/progress/client-ui-modernization/`

## Pas livré (volontaire)

- [ ] Thème sombre/or appliqué
- [ ] Host overlay
- [ ] HUD joueur / minimap / hotbar
- [ ] Fenêtres inventaire / perso / quêtes / shop / dialogue restylées
- [ ] `UserSettings` / `OptionsForm` réels
- [ ] Isolation perf du `MapViewRenderer` (full-map `Bitmap` à chaque dirty tick)

---

## Relation aux autres chantiers

| Chantier | Relation |
| --- | --- |
| Phase 7–8 (gameplay / quêtes) | **ACCEPTED on main** — fonctionnalités à **conserver** ; ce chantier ne change que la présentation |
| Phase 9 (distribution / admin) | Merge `f74b34c` sur `main` — hors scope UI |
| Phase 10 / PR #8 | **Interdit** sur cette branche |
| P9-S guildes / groupes / trades | **DEFERRED** — le mockup les montre ; le client ne les implémente pas ici |

`docs/STATUS.md` (racine) reste le statut Phase 9. Ce fichier est le statut **uniquement** du chantier UI client.

---

## Prochaine action (run suivant, pas celui-ci)

Étape **UI-1** du [STEP_PLAN.md](STEP_PLAN.md) : jetons de thème + application chrome existant **sans** changer le layout ni les `*ForTest`.
