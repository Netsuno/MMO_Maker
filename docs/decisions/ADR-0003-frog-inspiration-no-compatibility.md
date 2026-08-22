# ADR-0003 — FRoG : inspiration fonctionnelle, aucune compatibilité

- Statut : accepté
- Date : 2026-08-22
- PRD : `PRD_MMO_Maker_CSharp.md` v2.1

## Contexte

Une précédente feuille de route traitait le dépôt comme une migration FRoG (formats `.fcc`, enums VB6, importeur legacy). Le produit cible est un **MMO Maker** moderne inspiré fonctionnellement de FRoG et ergonomiquement de RPG Maker.

## Décision

- FRoG OSE 0.6.3 = **catalogue d’idées** (cartes, warps, PNJ, objets, etc.), pas un contrat technique.
- **Aucune** compatibilité binaire `.fcc`, protocole VB6, UDT, `Data1/2/3` ou enums historiques n’est requise pour livrer.
- Le modèle C#, PostgreSQL et le protocole moderne sont la source de vérité.
- `Frog.Legacy` et les fixtures `.fcc` = **expérimentation différée**, hors chemin critique.
- Aucun `Frog.LegacyImporter` ni golden master FRoG dans le backlog actif.

## Conséquences

- Prochaines tâches : shell éditeur → Map Editor MVP → playtest (pas d’import FRoG).
- Les docs actives ne doivent plus présenter l’import `.fcc` comme condition de livraison.
- Le code Legacy peut rester dans la solution tant qu’il compile et n’est référencé que par des tests Legacy.
