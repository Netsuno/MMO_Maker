# ADR-0004 — Coque éditeur WPF + îlots WinForms (temporaire)

- Statut : accepté (temporaire)
- Date : 2026-08-22

## Contexte

Le PRD cible **WinForms** pour l’éditeur. L’existant utilise déjà une **coque WPF** (`MainWindow.xaml`, panneaux project/tileset) qui héberge des contrôles WinForms (canevas GDI+, PropertyGrid, dialogues) via `WindowsFormsHost`.

## Décision

| Option | Verdict |
| --- | --- |
| Réécrire immédiatement tout en WinForms pur | **Rejeté** pour Phase 2/3 (coût élevé, risque UI) |
| Conserver la coque WPF actuelle | **Accepté temporairement** |
| Interdire toute nouvelle surface WPF hors coque/panneaux existants | **Accepté** |

Règles :

1. La coque et les panneaux de navigation/palette peuvent rester WPF.
2. Le canevas de carte et les dialogues métier restent WinForms (ou contrôles déjà hébergés).
3. Aucune logique métier / DB dans le code-behind WPF ou les formulaires.
4. Phase 3 construit le workspace RPG Maker–like **sur cette coque**, sans migration massive.
5. Réévaluer un retrait WPF seulement après le Map Editor MVP, avec ADR de révision.

## Conséquences

- Smoke UI Windows obligatoire (job CI ou manuel documenté) avant de déclarer le shell terminé.
- Hybride WinForms/WPF = dette connue, isolée, pas étendue.
