# Quickstart auteur

À la fin de ce guide, tu as une carte enregistrée, publiée, et un joueur peut la voir (même monde que le serveur).

Éditeur = **Windows 11 x64**. Menu Playtest WinForms *not proven on Linux agents* (hors gate).

Accès monde **protégé** — jamais le secret PostgreSQL aux joueurs. Overlay `appsettings.Local.json` = machine auteur / ops seulement.

> Zip serveur : Hello hors dépôt (`packaged-playtest-e2e.sh`). Recette éditeur distant 2 PCs : **accepted by owner Marc Giroux on 2026-09-19** (non rejouée dans cette PR).

## Prérequis

- Archive `editor-win-x64.zip` (protocole **v11**, même génération que serveur + client)
- Layouts frères recommandés : `../client-win-x64`, `../server-win-x64` (voir `EditorFrogClientLauncher` / `EditorFrogServerLauncher`)
- Accès workspace / publication fourni par l’opérateur
- Tuiles de base du projet (pas d’asset FRoG sans droits)

## 1. Ouvrir l’éditeur

Dézipper **hors** dépôt git. Lancer `Frog.Editor.exe`. SmartScreen possible (binaire non signé).

Smoke CI (shell puis quit, **pas** une session d’édition) :

```powershell
./scripts/packaged-winforms-smoke.ps1
```

<!-- CAPTURE: assets/auteur-01-accueil.png -->
*Capture à venir : fenêtre principale — menus, panneaux, zone carte.*

## 2. Créer une carte

**Fichier** → **Nouvelle carte**. Nom court, taille raisonnable.

<!-- CAPTURE: assets/auteur-02-nouvelle-carte.png -->
*Capture à venir : dialogue nom + taille.*

## 3. Peindre et habiller

1. Couche sol → tuile → peindre une zone.
2. Collisions / warps vers une autre carte du même monde.
3. NPC, objets, dialogue, quête, recette, événement **via les formulaires** (pas de JSON/SQL obligatoire).

Le monde démo de validation (3 cartes Village / Faubourgs / Arène) est une **fixture**, pas ton contenu final — [`../DEMO_WORLD.md`](../DEMO_WORLD.md).

<!-- CAPTURE: assets/auteur-03-peinture.png -->
*Capture à venir : carte + palette + couche active.*

## 4. Enregistrer / rouvrir

**Fichier** → **Enregistrer**. Fermer et rouvrir la carte (le deadlock d’ouverture `LoadPlacementsForMap` est **corrigé** : travail hors SynchronizationContext UI).

<!-- CAPTURE: assets/auteur-04-save.png -->
*Capture à venir : barre de titre / confirmation de sauvegarde.*

## 5. Publier

Publier vers PostgreSQL par le chemin éditeur supporté (`SaveAsync` / `Intent=Publish`). Masquer toute chaîne de connexion à l’écran et dans les tickets.

Un joueur connecté au **même** serveur doit voir le warp / le NPC après publication (recette étape 9 : **accepted by owner** pour le cas distant).

Publication fixture (ops / CI, pas ton monde final) :

```bash
./scripts/publish-demo-world.sh
```

## 6. Playtest depuis les binaires livrés

- **Prouvé CI :** serveur zip hors dépôt → Hello TCP ; Linux headless READY spawn ; Windows layouts frères + Hello. Voir [`../P10-3-PACKAGED-PLAYTEST.md`](../P10-3-PACKAGED-PLAYTEST.md).
- **Menu Playtest WinForms** (éditeur → spawn client) : **not proven on Linux agents**. Sur Windows, layouts frères `../client-win-x64` / `../server-win-x64`.
- Wine n’est **jamais** un pass.

## Tu es prêt si…

- [ ] Une carte nommée s’ouvre après fermeture
- [ ] Tuiles peintes (pas une carte vide)
- [ ] Publish sans éditer SQL / JSON à la main
- [ ] Un client du même build voit le contenu publié (loopback CI **ou** recette 2 PCs acceptée propriétaire)

## Et après ?

- [Périmètre bêta](../BETA_SCOPE.md) — ton contenu final ≠ fixture démo
- [Plan de recette](BETA_TEST_PLAN.md)
- Wiki : [Auteur](https://github.com/Netsuno/MMO_Maker/wiki/Auteur)
