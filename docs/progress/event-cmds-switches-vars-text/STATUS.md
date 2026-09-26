# STATUS — Commandes d'événement (interrupteurs, variables, branche, texte)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Palette éditeur + boîte de texte en jeu pour le pack MVP |
| **Propriétaire** | Netsun |
| **Statut** | MVP |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** — `.fmap` TileAsset **reste v6 / 48 px** |
| **PR** | Draft [#86](https://github.com/Netsuno/MMO_Maker/pull/86) vers `main` |

Les commandes `show_text`, `set_switch`, `set_variable`, `add_variable`, `sub_variable` et `branch` étaient déjà le catalogue Phase 8 (validation, exécution serveur, Postgres). Ce lot les rend posables depuis l'éditeur et lisibles en jeu.

## Livré

1. **Palette** — dans l'éditeur de pages et dans le alors / sinon d'une branche : Texte, Interrupteur, Variable =, Variable +, Variable −, Si interrupteur, Si variable. Le JSON stocké ne change pas. « Ajouter une commande » reste là pour le reste du catalogue.
2. **Branche** — interrupteur oui/non, ou variable avec `eq` `ne` `lt` `lte` `gt` `gte`. La liste affiche la condition.
3. **Exécution** — inchangée : le serveur applique interrupteur et variable dans la transaction Postgres, et renvoie le dernier `show_text` dans `InteractResult` (opcode 32).
4. **Boîte** — le client ouvre « Message » au-dessus de la hotbar. Entrée, Espace, la touche d'interaction ou un clic ferment. Le personnage ne bouge pas tant que le message est ouvert. Le nom de placement, `[Page]`, `[Marche]`, « Ramasse. » et l'écho du dialogue ouvert restent dans le journal.

Plusieurs `show_text` dans la même activation : la boîte montre le dernier. Une page parallèle réaffiche son texte à chaque battement de cœur.

## Commandes

| Palette | Discriminator | Effet |
| --- | --- | --- |
| Texte | `show_text` | Boîte « Message » |
| Interrupteur | `set_switch` | Oui / non sur la clé perso |
| Variable = / + / − | `set_variable` / `add_variable` / `sub_variable` | Entier perso |
| Si interrupteur | `branch` + `character_switch` | Alors / sinon |
| Si variable | `branch` + `character_variable_compare` | `eq` `ne` `lt` `lte` `gt` `gte` |

## Hors scope

Pictures, boutique, combat, trajet, presets d'événement, resize de carte, flags de tuile, panneau diagnostic, bump Hello, base de données.
