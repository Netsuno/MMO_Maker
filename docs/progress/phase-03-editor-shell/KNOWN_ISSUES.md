# Phase 03 — problèmes connus

| Sévérité | Item | Notes |
| --- | --- | --- |
| Moyenne | Serveur MariaDB : résolution Guid→int runtime via `RuntimeMapIdToGuid` | Héritage ; Phase 5+ |
| Faible | Hybride WPF/WinForms | ADR-0004 |
| Faible | Menus MariaDB héritage | Gelés |
| Faible | Save fichier `.fmap` v5 | Save PG = Phase 4 |

## Résolu (gate)

- Smoke Windows : PASS CI run 32575250906
- LegacyId actif : remplacé par `MapId` (migration forward)
