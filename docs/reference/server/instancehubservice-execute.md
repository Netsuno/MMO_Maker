# instancehubservice-execute

← [Server](README.md) · Tip : `a6edd821` · #33 scaffolding in-memory

Exécute Query / Enter / Leave instance. **Pas de `map_instance` PG.**

*Source : `InstanceHubService.Execute`* (sync)

**Signature :** `execute(session, kind, action, requestId, extra)`

**Entrées :**
- `session` — personnage actif
- `kind` Dungeon/Raid ; `action` Query/Enter/Leave
- Enter : `extra` = definitionId

**Sorties :**
- `(Result, Snapshot?)` — catalogue / run ; Enter crée ou rejoint ; Leave restaure overworld (hook session)

**Refus / gates :**
- pas de perso / action inconnue
- groupe requis ; raid **min 2** ; chef crée, membres rejoignent

**Exemple :**
```csharp
var (result, snapshot) = instanceHub.Execute(
    session, InstanceHubKind.Dungeon, action: 1 /* Query */, requestId, extra);
if (!result.Success) return; // gate groupe / action inconnue
```

**Honnêteté :** in-memory ; redémarrage serveur perd les runs.
