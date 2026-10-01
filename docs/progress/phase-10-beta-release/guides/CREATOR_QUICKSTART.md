# Quickstart auteur

> Disposition, ligne de statut, inspecteur et démarrage : [espace de travail](../../../EDITOR_WORKSPACE.md). Audit des contrôles : [inventaire UI](../../../EDITOR_UI_INVENTORY.md).

À la fin de ce guide, tu as peint une carte. Elle est enregistrée et publiée **si PostgreSQL répond**. Sinon l’édition reste locale et n’est pas enregistrée.

Hello / protocole **v11**. Tuiles `TileAsset` **48×48**. Éditeur = **Windows 11 x64**.

Menu Playtest WinForms manuel : **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** (`--smoke-launch` Windows CI distinct).

Accès monde **protégé** — jamais le secret PostgreSQL aux joueurs. Overlay `appsettings.Local.json` = machine auteur / ops seulement.

> Zip serveur : Hello hors dépôt (`packaged-playtest-e2e.sh`). Recette éditeur distant 2 PCs : **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** (non rejouée dans cette PR).

## Prérequis

- Archive `editor-win-x64.zip` (protocole **v11**, même génération que serveur + client)
- Layouts frères recommandés : `../client-win-x64`, `../server-win-x64` (voir `EditorFrogClientLauncher` / `EditorFrogServerLauncher`)
- Accès workspace / publication fourni par l’opérateur
- Tuiles de base du projet (pas d’asset FRoG sans droits)

## 1. Ouvrir l’éditeur

Dézipper **hors** dépôt git. Lancer `Frog.Editor.exe`. SmartScreen possible (binaire non signé).

La fenêtre `MMO Maker — Éditeur` s’ouvre tout de suite (minimum 1280×720, maximisée). Le catalogue part sur `Task.Run` : le thread de l’interface reste libre.

| Ce que PostgreSQL fait | Ce que tu vois |
| --- | --- |
| Il répond | Enregistrer et Publier peuvent s’activer. Pas d’avis hors ligne |
| Il est configuré et injoignable (socket, délai, E/S, Npgsql) | Repli `InMemoryDemo`. La ligne de statut **commence** par `PostgreSQL injoignable — édition locale (non enregistrée)`. Enregistrer et Publier restent inactifs |
| Aucune chaîne de connexion | Même mémoire démo, **sans** cet avis. Enregistrer et Publier inactifs |
| `FROG_EDITOR_FORCE_IN_MEMORY=1` (smoke) | Mémoire de test, **sans** cet avis |

Les libellés WPF restent **Enregistrer (PostgreSQL)** et **Publier (PostgreSQL)…** même quand les commandes sont inactives. Le suffixe catalogue en démo est `mémoire (démo — non persistant)`.

Smoke CI (shell puis quit, **pas** une session d’édition) :

```powershell
./scripts/packaged-winforms-smoke.ps1
```

<!-- CAPTURE: assets/auteur-01-accueil.png -->
*Capture à venir (NET-11) : fenêtre — barre d’icônes, arbre des cartes, canevas, radios du rail droit.*

## 2. Lire la fenêtre

- Barre d’icônes : Nouvelle, Ouvrir, Enregistrer, Annuler, Rétablir, Zoom +/−, Tester.
- Gauche (~200 px) : arbre des cartes seulement.
- Centre : canevas. Minimap dans le coin haut-droit. Le bandeau de titre de carte est masqué.
- Droite : radios **Tuiles | Objets | Entités | Régions**, puis le contenu du mode. Couches (~168 px) toujours visibles. Inspecteur replié tant qu’il n’y a rien à inspecter (~160 px une fois ouvert).

## 3. Créer une carte

**Fichier** → **Nouvelle carte…** (ou l’icône Nouvelle). Nom court, taille raisonnable.

<!-- CAPTURE: assets/auteur-02-nouvelle-carte.png -->
*Capture à venir : dialogue nom + taille.*

## 4. Peindre (radio Tuiles)

1. Laisse ou choisis la radio **Tuiles**.
2. Dans le dock Couches (toujours là), sélectionne la couche. Visibilité, verrou, opacité et « atténuer les autres » sont dans ce dock.
3. Choisis une tuile sur la feuille, sous les outils.
4. **Pinceau** (B) : clic ou glisser. Gomme (E), Remplissage (F), Rectangle (R), Ligne (L) sont dans la même radio. Pipette : bouton **Pipette**, touche I, ou Alt+clic.

Le type de tuile (blocage, warp, ressource, script) est sous la radio **Régions**, avec la puce Région (G).

<!-- CAPTURE: assets/auteur-03-peinture.png -->
*Capture à venir : radio Tuiles, feuille, couche active, canevas.*

Le monde démo de validation (3 cartes Village / Faubourgs / Arène) est une **fixture**, pas ton contenu final — [`../DEMO_WORLD.md`](../DEMO_WORLD.md).

## 5. Objets, entités, régions

**Objets.** Choisis un objet dans la grille (filtre au-dessus). Le facing n’apparaît que s’il y a plusieurs variantes. Ce choix arme la pose : clic pour placer, clic droit pour effacer, Ctrl+D pour dupliquer, Échap pour quitter. Le bouton **Placer un objet** n’est pas affiché.

**Entités.** Départ, PNJ, Coffre, Porte, Auberge, puce Entités. Outil Entités (N) : clic pose, glisser déplace, clic droit retire. PNJ rapide, coffre, porte et auberge demandent PostgreSQL, puis un clic sur la carte.

**Régions.** Type de tuile + Région (G) : peint 1–63, 0 efface, les numéros restent dessinés sur la carte. Le panneau Régions / Rencontres s’ouvre dans l’inspecteur.

Données de jeu plus larges : menu **Ressources → Données de jeu…** (formulaires). Pas de JSON ni de SQL à écrire à la main. Hors ligne, ces dépôts passent en mémoire démo.

## 6. Sélection

Outil **Sélection** (M), dans la radio Tuiles.

- Glisser le clic **gauche à l’intérieur** du rectangle **déplace** la zone.
- Glisser le clic **droit** **copie** la zone.
- Chaque geste est un pas d’annulation. Ce tampon ne passe pas par Ctrl+C.
- Les numéros de région restent où ils sont. Maj limite le geste à la couche active.
- Glisser **en dehors** du rectangle retrace la sélection. Clic droit en dehors l’efface.
- Double-clic : la pipette prend la tuile **sans** changer d’outil (le premier demi-clic de pinceau, de pot, de rectangle ou de ligne est annulé).

Ctrl+C / Ctrl+X / Ctrl+V copient encore toutes les couches ; Ctrl+Maj limite à la couche active. Détail : [`../../editor-selection-tools/STATUS.md`](../../editor-selection-tools/STATUS.md).

## 7. Ligne de statut et inspecteur

Une seule ligne, dans cet ordre :

`mode · outil · couche · (x, y) · tuile (a, b) · zoom N %`

Avant le premier survol, la paire monde est `(0, 0)`. La suite de la ligne ajoute révision, modifié, enregistrement, départ, compteurs et catalogue. Il n’y a pas de second bandeau.

L’inspecteur reste replié (hauteur 0) pendant un clic pinceau. Il s’ouvre (~160 px) pour l’outil Région, pour une entité posée ou l’outil Entités, ou pour une tuile inspectée avec le curseur (C). Les couches (~168 px) restent affichées.

## 8. Enregistrer

**Fichier → Enregistrer (PostgreSQL)** ou l’icône Enregistrer, **seulement si PostgreSQL répond** (`AllowsSave`). Ferme puis rouvre la carte : le deadlock d’ouverture `LoadPlacementsForMap` est corrigé (travail hors du SynchronizationContext UI).

Si la ligne commence par `PostgreSQL injoignable — édition locale (non enregistrée)`, l’enregistrement est refusé : la session est locale et non persistée. Sans chaîne de connexion, même refus, sans cette phrase.

<!-- CAPTURE: assets/auteur-04-save.png -->
*Capture à venir : ligne de statut après un enregistrement PostgreSQL.*

## 9. Publier

**Fichier → Publier (PostgreSQL)…** (`MainForm.PublishMap` → `SaveCurrentAsync` / `SaveMapIntent.Publish`), même condition : PostgreSQL doit répondre. Masque toute chaîne de connexion à l’écran et dans les tickets.

Un joueur connecté au **même** serveur doit voir le warp / le PNJ après publication (recette étape 9 : **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.**).

Publication fixture (ops / CI, pas ton monde final) :

```bash
./scripts/publish-demo-world.sh
```

## 10. Playtest depuis les binaires livrés

- **Prouvé CI :** serveur zip hors dépôt → Hello TCP ; Linux headless READY spawn ; Windows layouts frères + Hello. Voir [`../P10-3-PACKAGED-PLAYTEST.md`](../P10-3-PACKAGED-PLAYTEST.md).
- **Menu Playtest WinForms** (éditeur → spawn client) : **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** Layouts frères `../client-win-x64` / `../server-win-x64`.
- Wine n’est **jamais** un pass.

## Tu es prêt si…

- [ ] La radio **Tuiles** peint une zone sur la couche choisie
- [ ] La ligne de statut unique affiche mode, outil, couche, position et zoom
- [ ] PostgreSQL répond : la carte nommée se rouvre après fermeture, et Publier ne passe pas par du SQL ou du JSON écrit à la main
- [ ] PostgreSQL est injoignable : l’avis exact `PostgreSQL injoignable — édition locale (non enregistrée)` est en tête de ligne, et Enregistrer / Publier sont inactifs
- [ ] Un client du même build voit le contenu publié (loopback CI **ou** recette 2 PCs acceptée propriétaire)

## Et après ?

- [Espace de travail](../../../EDITOR_WORKSPACE.md) — gabarit, statut, inspecteur, démarrage
- [Périmètre bêta](../BETA_SCOPE.md) — ton contenu final ≠ fixture démo
- [Plan de recette](BETA_TEST_PLAN.md)
- Wiki `Auteur` : miroir séparé, pas encore aligné sur ce guide (suivi hors de ce dépôt)
