# STATUS — Éditeur : spawn + outils manquants

| Champ | Valeur |
| --- | --- |
| **Chantier** | Point de départ playtest + petits écarts d’outils éditeur |
| **Propriétaire** | Netsun |
| **Statut** | Spawn cliquable + mémo workstate + raccourcis / marqueur — **pas de merge** |
| **Base** | `main` @ `deadcac` (merge PR #20 DA v2 status portrait) |
| **Branche** | `cursor/editor-spawn-and-tools-gap-a846` |
| **PR** | Draft [#22](https://github.com/Netsuno/MMO_Maker/pull/22) vers `main` — **pas de merge** |
| **Tip** | `0e1b951` |
| **CI** | [35521557132](https://github.com/Netsuno/MMO_Maker/actions/runs/35521557132) **SUCCESS** (`build-and-test` + `postgres-integration`) |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** — pas de bump `.fmap` |

Parallèle à #21 (icônes menu client). **Aucun** changement Frog.Client HUD / `Hud*` / `MainShellForm`. Prefabs objets carte **hors scope**.

---

## Spawn / point de départ (livré)

Au-delà de `PlaytestSpawnDialog` (X/Y seuls) :

1. **Outil Départ (`EditorTool.Spawn`, touche D)** — clic gauche sur une tuile pose le spawn playtest. Pas de peinture, couche verrouillée ignorée, clic droit n’efface pas.
2. **Marqueur canvas** — losange cyan + contour pointillé sur la tuile de spawn (distinct des warps / pastilles événements).
3. **Persistance** — `editor-workstate.json` champ additif `mapPlaytestSpawns` :
   - clé `id:{guidN}` si la carte a un `MapId` catalogue ;
   - sinon `local:{nom}|{W}x{H}` (brouillon / `.fmap` fichier).
   Réouverture de la même carte restaure la tuile. Enregistrement / publish recopie la clé sous le nouvel id.
4. **Playtest** — le dialogue est prérempli avec le spawn mémorisé (repli : survol). `OverrideSpawnTile` inchangé. `PlaytestSpawnValidator` toujours appliqué au lancement.

UI FR : palette « Placer le départ (D) », menu **Carte → Outil point de départ**, bouton barre **Départ**, statut `départ (x,y)`.

---

## Outils — audit vs `EditorTool` actuel

| Outil | Palette | Canvas | Raccourci livré | Écart restant |
| --- | --- | --- | --- | --- |
| Pinceau | oui | oui | **B** | — |
| Gomme | oui | oui | **E** | — |
| Curseur | oui | inspecte un clic | **C** | pas de déplacement d’objets (pas de prefabs) |
| Pot | oui | flood 4-dir | **F** | — |
| Rectangle | oui | clic–clic | **R** | — |
| Sélection | oui | copier/coller couche | **M** | pas de rotation / miroir |
| Départ | **ajouté** | clic spawn | **D** | — |

`Form1` n’est qu’un stub designer : les raccourcis réels sont `MainForm.ProcessCmdKey` + `MapCanvas.HandleEditorShortcuts` + `MainWindow`.

**Livré (sûr, non conflictuel)** : raccourcis lettres sans modificateur (pas de conflit Ctrl+C/X/V/Z/Y/N/O/S, Ctrl+F5, Échap, Suppr) ; libellés palette avec touche ; hint + bouton Départ ; marqueur spawn.

---

## Différé (volontaire)

- Prefabs / objets carte posables, pipette, ligne, cercle, tampon multi-cartes.
- Spawn **dans** le blob `.fmap` / publish world (`world_spawn_settings`) — volontairement workstate local, pas de bump fichier ni fil.
- Éditeur de spawn monde serveur (start/respawn globaux) : déjà une table PG singleton, pas ce chantier.
- Phase 6–8 (Game Data, événements, quêtes) : **non touchés**.
- Assets prefab : pipeline #14 déjà mergé ; pas d’ajout d’objets ici.

---

## Tests

- Unitaires Linux : `Frog.Tests/MapPlaytestSpawnTests.cs` (clé workstate, clamp, ResolvePreferred + validateur playtest).
- Smoke Windows : raccourcis, workstate JSON isolé, clic spawn sans peinture, restore + hotkey sur `MainForm`.
- Playtest existant : `OverrideSpawnTile` toujours honoré.

CI **SUCCESS** sur `0e1b951` : `build-and-test` (smokes Windows inclus) + `postgres-integration`. Pas de job assoupli.

Vérifié aussi en local (Linux) : `dotnet test Frog.Tests` — **585 passed**, 0 skipped.
