# Captures DA — Windows only

**Propriétaire :** Netsun

Linux (agents cloud / ce run) **ne peut pas** lancer WinForms ni produire les PNG HUD. Compile `EnableWindowsTargeting` seulement. Les captures ci-dessous sont le **blocage revue pixel** (ambre jusqu’à exécution Windows).

## Résolutions / DPI

| Fenêtre client | DPI Windows | Dossier |
| --- | --- | --- |
| 1280×720 | 100 % | `artifacts/client-ui-modernization/1280-dpi100/` |
| 1920×1080 | 100 % | `artifacts/client-ui-modernization/1920-dpi100/` |
| 1280×720 | 125 % | `artifacts/client-ui-modernization/1280-dpi125/` |
| 1920×1080 | 125 % | `artifacts/client-ui-modernization/1920-dpi125/` |

Script : [`scripts/capture-client-ui-da.ps1`](../../../scripts/capture-client-ui-da.ps1) (crée les dossiers + checklist ; ne lance pas le jeu tout seul).

## Cases (une PNG par case)

1. `01-shell-hud.png` — in-game, **onglets fermés**, carte plein cadre, 6 modules HUD
2. `02-status-combat.png` — Status avec `CombatState` réel (Lv court, HP/MP, pas de barre XP)
3. `03-minimap-quest.png` — minimap + tracker
4. `04-chat-channels.png` — dock chat multi-canaux (couleurs owner-draw)
5. `05-hotbar-menu.png` — hotbar chiffres + menu BD
6. `06-inventory-gold.png` — Inv ouvert (Menu → Inv), double filet or ; carte encore visible à gauche
7. `07-options-reseau.png` — Options nav Réseau

Ne **pas** capturer Dialogue / Quest journal / Environment pour le SHA Phase 8 (`02`–`04` exact-sha).

## Comment

1. Machine Windows, DPI 100 puis 125 (Paramètres → Échelle).
2. `Frog.Client` en Playing (serveur local). Options → Graphisme : largeur/hauteur 1280×720 puis 1920×1080.
3. `pwsh -File scripts/capture-client-ui-da.ps1` puis Win+Shift+S / outil habituel vers le dossier indiqué.
4. Relire [TOKENS-DA.md](TOKENS-DA.md) § contraste or / texte. Pas de chiffre FPS inventé.
