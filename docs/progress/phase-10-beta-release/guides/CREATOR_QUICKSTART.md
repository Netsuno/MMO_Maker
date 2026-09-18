# Guide rapide — Auteur de confiance

> **Statut : BROUILLON P10-0 — Phase 10 pas prête. Ne pas diffuser comme procédure livrée.**

Vous recevez l’**éditeur** Windows et une procédure d’accès **protégée** au monde. Les joueurs ne reçoivent pas cet accès.

## Objectif

Créer / modifier / publier du contenu (cartes, PNJ, objets, quêtes, dialogues, recettes, événements) **sans** éditer du JSON ou du SQL à la main.

<!-- CAPTURE: assets/creator-01-editor-home.png — fenêtre principale éditeur ; sans DSN visible -->
*Capture à venir : accueil éditeur.*

## Déjà disponible (from-source / Phases 6–9)

- Éditeurs structurés Phase 6–8 (tilesets, PNJ, objets, sorts, classes, boutiques, ressources, contenu Phase 8).
- Publication PostgreSQL réelle pour cartes et catalogues (voir rapports Phase 6–8).
- Fermeture non coopérative / close-during-save : acquis Phase 8–9 à préserver.

Référence packaging (layouts) : [`../../phase-09-distribution-admin-hardening/PACKAGING_GUIDE.md`](../../phase-09-distribution-admin-hardening/PACKAGING_GUIDE.md).

## Pas encore prêt

| Attente auteur | État |
| --- | --- |
| Paquet éditeur autonome + lancement prouvé depuis l’arbre publié | **Non prouvé** (P10-6) |
| Playtest depuis binaires livrés (pas chemins `bin/Debug` du dépôt) | **Non prouvé** |
| Monde démo 3 cartes 30–60 min redistribuable | **Absent** (P10-4) |

Le **contenu final** du jeu (cartes définitives, dialogues, quêtes de sortie) reste la création de Marc. La Phase 10 livre les outils + un monde de démonstration temporaire pour la recette.

<!-- CAPTURE: assets/creator-02-publish.png — dialogue publier ; masquer connection string -->
*Capture à venir : publication (aucun secret à l’écran).*

## Règles de sécurité

1. `appsettings.Local.json` de l’éditeur = accès monde-admin. **Ne jamais** le committer ni le partager aux joueurs.
2. Ne pas coller de DSN dans le wiki, les tickets ou le chat public.
3. Préférer les formulaires structurés ; ne pas dépendre d’édition JSON manuelle.

## Liens

- [`../BETA_SCOPE.md`](../BETA_SCOPE.md) § auteur / outils
- Wiki : [Auteur](https://github.com/Netsuno/MMO_Maker/wiki/Auteur)
