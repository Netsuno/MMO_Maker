# Premier monde en une session

Guide minimal pour avoir **carte jouable** + **éditeur** + **client** sur la même machine avec le dépôt actuel (`Frog.Creator.sln`).

## Prérequis

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installé et accessible en ligne de commande.

## Contrats à connaître

| Constante | Fichier / type | Rôle |
|------------|-----------------|------|
| `FrogWireProtocol.Version` | `Frog.Core` | Envoyée dans **`Hello`** (UInt16 LE). Client et serveur doivent correspondre ou le client coupe la connexion avec un message d’erreur. |
| `MapSerializer.MapFileFormatVersion` | `Frog.Core` | Premier octet de version après le magic `FMAP` dans tout fichier `.fmap` ou blob `MapData`. Doit suivre celui compilé avec le jeu. |

Détail réseau : [`Frog.Client/Docs/protocol_login_map.md`](../Frog.Client/Docs/protocol_login_map.md).

## 1. Compiler

À la racine du dépôt :

```bash
dotnet build Frog.Creator.sln
dotnet test Frog.Tests/Frog.Tests.csproj
```

## 2. Créer ou réutiliser un `.fmap`

```bash
dotnet run --project Frog.Editor/Frog.Editor.csproj
```

- Crée une carte, charge un tileset (PNG), puis **Enregistrer** au format `.fmap` (ex. `world.fmap`).
- L’éditeur écrit aussi **`world.tilesets.json`** à côté (liste `id` → nom de fichier PNG). Copiez **ce JSON et les PNG** utilisés vers le client pour le même rendu qu’en édition.

### Tilesets côté client (PNG)

À côté de l’exe du client (`…/Frog.Client/bin/Debug/net8.0/` en dev) :

1. Dossier **`Maps/`** : placez **`{NomDeLaCarte}.tilesets.json`** (le `Map.Name` de la carte, caractères invalides remplacés par `_`) + les fichiers PNG référencés dans le JSON (même dossier que le JSON en général).
2. **Ou** dossier **`Tilesets/`** : fichier **`manifest.json`** (même schéma JSON que l’éditeur) + PNG à côté.
3. **Ou** fichiers **`Tilesets/{id}.png`** pour chaque `TilesetId` utilisé sur la carte (si pas de manifeste).

Sans PNG trouvé, le client affiche encore la carte en **couleurs de secours** par type de tuile.

Sans fichier personnalisé côté **serveur**, la carte de secours intégrée suffit pour tester tout de suite.

## 3. Option A — MariaDB désactivée (léger)

Dans [`Frog.Server/appsettings.json`](../Frog.Server/appsettings.json) :

```json
"MariaDb": { "enabled": false },
"Maps": { "worldMapPath": "Maps\\\\world.fmap" }
```

Ou laisse `"worldMapPath": ""` pour la carte interne.

Compte **mémoire** inclus : utilisez **`demo`** / **`demo`** (créés côté serveur en développement). Vous pouvez aussi **Créer un compte** depuis le formulaire du client puis vous connecter.

## 4. Option B — MariaDB (MySQL)

1. Lancez MariaDB (ou MySQL) avec une base vide (locale ou Docker).
2. `MariaDb.enabled` → `true` et `connectionString` valide dans `appsettings.json` ou `appsettings.Local.json`.
3. Au premier démarrage serveur, un compte `demo`/`demo` peut être créé automatiquement si absent (voir `MariaDbAccountRepository` / `MariaDbSchemaBootstrap`).

Sans Docker : installez MariaDB, créez une base `frog`, accordez les droits à l’utilisateur et mettez la chaîne de connexion au format `Server=...;Port=3306;Database=...;User Id=...;Password=...`.

## 5. Lancer dans l’ordre

**Terminal 1 — serveur** (répertoire d’exe = sortie typique sous `Frog.Server/bin/Debug/net8.0/` ; les chemins **relatifs** dans `Maps:worldMapPath` sont relatifs à ce dossier) :

```bash
dotnet run --project Frog.Server/Frog.Server.csproj
```

Copiez le `.fmap` dans `Frog.Server/bin/Debug/net8.0/Maps/` si vous utilisez `"Maps\\world.fmap"`, ou mettez un **chemin absolu** dans la config pour éviter l’erreur « fichier introuvable ».

**Terminal 2 — client** :

```bash
dotnet run --project Frog.Client/Frog.Client.csproj
```

- Hôte `127.0.0.1`, port **`6000`** par défaut (voir `appsettings.json` → `Server.port`).
- Connectez‑vous puis déplacez-vous avec les flèches ; le chat global / carte / whisper est déjà disponible.

## Éditer et recharger la carte monde

Pour voir vos changements de `.fmap` sur le terrain **monde serveur**, redémarrez le serveur après avoir remplacé le fichier sous le chemin configuré. (Rechargement à chaud optionnel futur.)

## Dépannage rapide

- **Erreur de version protocole** au connect : même branche/build pour `Frog.Client` et `Frog.Server` (`FrogWireProtocol.Version`).
- **Map invalide** au login : fichier `.fmap` produit avec un autre projet ou ancienne série — régénérez avec l’**éditeur du même dépôt** ou videz `worldMapPath` pour tester avec la carte de secours.
