# STATUS — Menus éditeur MariaDB / SQL

| Champ | Valeur |
| --- | --- |
| **Chantier** | Retirer les entrées de menu MariaDB / SQL devenues obsolètes |
| **Propriétaire** | Netsun |
| **Statut** | Scrub éditeur |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** |
| **Tuiles** | TileAsset **48×48**, carte **v6** |

PostgreSQL reste le chemin d’enregistrement, de publication et d’événements. Le départ playtest et les entités posées restent un mémo local (`editor-workstate.json`), sans SQL.

## Retiré

- Menu **Fichier → Publier vers MariaDB… (héritage)** (coque WPF et menu WinForms) et le dialogue qui allait avec.
- Libellés de commande encore marqués « MariaDB, héritage » sur **Événements carte** et **Actualiser marqueurs événements**. Les menus affichés disaient déjà le texte PostgreSQL.
- Code qui ne servait que ces menus : publication `frog_map`, lecture / écriture MariaDB des événements carte, nommage `map_key`, exemple `MariaDb` de l’éditeur, paquet `MySqlConnector` de l’éditeur.
- Champ workstate `lastPublishedFrogMapId` (id MariaDB). Un ancien fichier se relit ; la clé n’est plus réécrite.

## Conservé

- **Enregistrer (PostgreSQL)**, **Publier (PostgreSQL)…**, catalogue, événements carte, contenu Phase 8.
- Outils **Départ (D)** et **Entités (N)**. Spawns de ressources dans Données de jeu (PostgreSQL).
- MariaDB côté serveur (schéma, bootstrap). Le crochet de test `SkipMariaDbOnStartup` garde son nom : il saute seulement le rechargement des marqueurs au démarrage.
- Overlay `MapEventMarkerView` (déplacé, plus dans le lecteur MariaDB).

Docs de référence et matrice MariaDB : pas de vague docs. Cette note suffit.
