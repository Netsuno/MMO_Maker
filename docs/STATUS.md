# État du projet FRoG

- Dernière mise à jour : 2026-08-22 02:58 UTC
- Branche / commit : `cursor/phase0-baseline-audit-02c7` (base `6df9f55` sur `main`)
- Phase active : **Phase 0 terminée → Phase 1 / Task 2**
- Dernier commit vert (historique `main`) : `6df9f55` — CI Windows success (2026-05-13)
- Environnement audit : Ubuntu 24.04, .NET SDK 8.0.424, PostgreSQL **absent**, MariaDB **non démarrée**, Docker **absent**

## Vérifié comme fonctionnel

- Restauration NuGet des 5 projets avec `-p:EnableWindowsTargeting=true`
- Compilation solution Release avec `-p:EnableWindowsTargeting=true -p:LangVersion=preview`
- `dotnet test Frog.Tests` : **82 réussis, 0 échoué, 0 ignoré** (mêmes flags ; pas de MariaDB)
- Sérialisation / validation carte `.fmap`, mouvement/warps mémoire, wire Hello & map events (couverts par les tests ci-dessus)

## Implémenté, mais non vérifié

- Éditeur WinForms/WPF (non exécuté sur Linux)
- Client WinForms (non exécuté sur Linux)
- Persistance MariaDB réelle (migrations v1–v10, repos) — test d’intégration no-op sans `MARIADB_TEST_CONNECTION_STRING`
- Publication éditeur → MariaDB
- Serveur en processus long / E2E TCP

## En cours

- Une seule tâche : **Task 2 — build/tests verts sous C# 12 sans `LangVersion=preview`**, plus documentation de commande unique (agents Linux : `EnableWindowsTargeting`)

## Bloqueurs

- **Produit :** PRD impose PostgreSQL ; le dépôt utilise MariaDB largement — décision humaine requise avant Task 4 / Phase 4
- Sources VB6 et fixtures `.map` absentes de ce dépôt (bloque Phase 2+)
- Pas de runtime WinForms dans cet environnement Cloud Linux

## Commandes de validation

```bash
# Agent Linux (état actuel, contournements)
export PATH="$HOME/.dotnet:$PATH"
dotnet restore Frog.Creator.sln -p:EnableWindowsTargeting=true
dotnet build Frog.Creator.sln -c Release -p:EnableWindowsTargeting=true -p:LangVersion=preview
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build

# CI (référence dépôt)
# windows-latest : dotnet restore/build Frog.Creator.sln ; dotnet test Frog.Tests
```

## Résultat de la dernière validation

- Build : **PASS** (avec `EnableWindowsTargeting` + `LangVersion=preview`) ; **FAIL** sans ces flags sur Linux
- Tests : **82 réussis, 0 échoués, 0 ignorés**
- Intégration PostgreSQL : **NOT RUN** (non présent)
- Intégration MariaDB : **NOT RUN** (env var absente)
- E2E : **NOT RUN**

## Prochaine tâche

- Corriger `PacketDispatcher` (pas de `Span` à travers `await`) pour compiler en C# 12 / `LangVersion` stable ; ancrer `EnableWindowsTargeting` pour builds non-Windows ; mettre à jour README/`docs/TESTING.md` avec la commande unique ; commit vert Task 2.

## Référence

- Audit détaillé : [`docs/BASELINE_AUDIT.md`](BASELINE_AUDIT.md)
- PRD d’exécution : `PRD_FRoG_Creator_Migration_CSharp.md` (fourni à l’agent)
