# STATUS — Client UI modernization

| Champ | Valeur |
| --- | --- |
| **Chantier** | Client UI modernization (présentation Frog.Client) |
| **Propriétaire** | Netsun |
| **Statut** | **PLAN / AUDIT ONLY** — pas de code produit HUD |
| **Base docs** | `main` @ `f74b34cca09dda819fe26747d48ee16d27007dfd` (cette branche n’embarque pas le code P10) |
| **Inventaire UI** | Lecture seule tip P10 `853776e0d75312458e47200cd5fa797ea96fd8f0` (client-engineer) |
| **Branche** | `cursor/client-ui-modernization` |
| **PR** | Draft https://github.com/Netsuno/MMO_Maker/pull/9 vers `main` — **pas de merge** |
| **Non-interférence** | Ne pas toucher `cursor/phase10-beta-release` / PR #8 ; pas de wire / TLS |

Protocole inchangé : `FrogWireProtocol.Version = 10`.

---

## Ce run (livré)

- [x] Analyse UI actuelle (`MainShellForm` + panneaux live + stubs historiques)
- [x] Inventaire : fonctionne / se conserve / présentation-only
- [x] Architecture overlay + thème sombre/or + services existants
- [x] Découpe en petites étapes indépendantes + critères done + risques 60 FPS
- [x] Docs dans `docs/progress/client-ui-modernization/`
- [x] Recalage inventaire P10 `853776e` (Options/Help/Trade/settings live ; stubs folklore ; mapping extraits)

## Pas livré (volontaire)

- [ ] Thème sombre/or appliqué
- [ ] Host overlay
- [ ] HUD joueur / minimap / hotbar
- [ ] Fenêtres inventaire / perso / quêtes / shop / dialogue restylées
- [ ] Restyle `OptionsForm` / `HelpForm` (déjà réels sur P10 — ne pas recréer le store)
- [ ] Isolation perf du `MapViewRenderer` (full-map `Bitmap` à chaque dirty tick)

---

## Relation aux autres chantiers

| Chantier | Relation |
| --- | --- |
| Phase 7–8 (gameplay / quêtes) | **ACCEPTED on main** — fonctionnalités à **conserver** ; ce chantier ne change que la présentation |
| Phase 9 (distribution / admin) | Merge `f74b34c` sur `main` — hors scope UI |
| Phase 10 / PR #8 | **Lecture seule** `853776e` pour l’inventaire UI. **Interdit** de modifier cette branche P10 depuis ici. Sur ce tip : Options/Help/Trade/settings + canaux Party/Guild **déjà** dans le combo. |
| Trade / Party / Guild | Présents sur `853776e` (UI + enum). Cette PR docs n’ajoute aucun opcode. |

`docs/STATUS.md` (racine) reste le statut Phase 9. Ce fichier est le statut **uniquement** du chantier UI client.

---

## Prochaine action (run suivant, pas celui-ci)

Étape **UI-1** du [STEP_PLAN.md](STEP_PLAN.md) : jetons de thème + application chrome existant **sans** changer le layout ni les `*ForTest`.
