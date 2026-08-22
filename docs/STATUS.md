# État du projet FRoG

- Dernière mise à jour : 2026-08-22 03:35 UTC
- Branche / commit : `cursor/phase0-baseline-audit-02c7`
- Phase active : **Phase 2→3 — Task 8/9 (modèle + LegacyFccMapReader)** ; Task 4 PG bloquée
- Environnement : Ubuntu 24.04, .NET SDK 8.0.424

## Vérifié comme fonctionnel

- Build/test C# 12 + architecture boundaries
- Inventaire VB6 (105/44/8 + 3 ctl)
- Caractérisation en-tête `.fcc` sur 3 fixtures (taille, nom, array 31×31, SHA-256 map1)

## Implémenté, mais non vérifié

- Éditeur / client WinForms ; MariaDB réelle ; E2E TCP
- Reader `.fcc` complet (test Skip Task 9)

## En cours

- Une seule tâche : **implémenter `LegacyFccMapReader` + modèle nécessaire (Tasks 8–9)** après commit de la spec/fixtures

## Bloqueurs

- **Task 4 :** PostgreSQL (PRD) vs MariaDB (dépôt) — décision humaine
- Détail octet-à-octet des champs `String`/`*Set` dans les 88 octets tuile (documenté comme À PROUVER)

## Commandes de validation

```bash
dotnet restore Frog.Creator.sln && dotnet build Frog.Creator.sln -c Release --no-restore && dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
```

## Résultat de la dernière validation

- À rafraîchir après tests characterization

## Prochaine tâche

- Frog.Legacy (ou Core temporaire) : reader `.fcc` header + tuiles + warps avec golden masters sur map1–3 ; rapport d’import structuré.
