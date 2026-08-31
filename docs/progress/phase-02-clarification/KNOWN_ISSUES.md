# Known issues — Phase 02

| ID | Sévérité | Symptôme | Reproduction | Portée | Contournement | Plan |
| --- | --- | --- | --- | --- | --- | --- |
| P2-UI-001 | moyenne | Éditeur non smoke-testé sur agent Cloud | Exécuter `Frog.Editor` sur Linux | UI | Tester sur Windows / CI windows | Phase 3 gate |
| P2-MDB-001 | moyenne | MariaDB encore branchable dans Editor/Server | `MariaDb.enabled=true` | DB héritage | Laisser disabled ; utiliser PG | Retrait domaine par domaine |
| P2-WPF-001 | moyenne | Hybride WPF+WinForms | Ouvrir éditeur | UI | ADR-0004 ; ne pas étendre WPF | Réévaluer post-MVP |
| P2-DOC-001 | faible | Sections historiques bas de README (jalons anciens) | Lire README bas | Docs | STATUS/BACKLOG font foi | Nettoyage progressif |
| P2-LEG-001 | faible | Tests Legacy `.fcc` toujours dans Frog.Tests | `dotnet test Frog.Tests` | Tests | Accepté (non-régression code différé) | Conserver ou isoler plus tard |

## Limites d’environnement

- Pas de Docker dans cet agent ; PostgreSQL 16 installé nativement pour les tests.
- WinForms/WPF non exécutables ici.
