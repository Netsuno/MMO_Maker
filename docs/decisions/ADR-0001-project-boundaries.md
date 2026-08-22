# ADR-0001 — Frontières de projets (état initial Phase 1)

- Statut : accepté
- Date : 2026-08-22
- Contexte : PRD migration C# ; solution existante à 5 projets

## Décision

Conserver le graphe `Editor/Client/Server → Core` et `Tests → Core+Server` jusqu’à l’introduction progressive des projets PRD (`Application`, `Legacy`, `Persistence.*`, `Rendering`, `Protocol`).

Les tests d’architecture verrouillent :

- pureté de `Frog.Core` (pas d’UI / DB / EF) ;
- absence de cycles ;
- pas d’accès SQL direct dans les surfaces UI éditeur (`Forms/`, `MainWindow.xaml.cs`).

Les adaptateurs MariaDB dans `Frog.Editor/Services` et `Frog.Server/Database` restent temporairement hors ports applicatifs.

## Conséquences

- Pas de renommage massif ni de création de projets vides « pour la structure ».
- La bascule PostgreSQL (PRD) reste un bloqueur produit distinct (`docs/STATUS.md`).
- Toute nouvelle dépendance Core vers UI/DB fera échouer `ArchitectureBoundaryTests`.
