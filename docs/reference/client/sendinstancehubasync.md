# sendinstancehubasync

← [Client](README.md) · [Référence](../README.md)

Envoie une enveloppe **InstanceHub** (opcodes 90–92) : Query / Enter / Leave.

*Source : `FrogGameClient.SendInstanceHubAsync`* · Tip : `a6edd821` · #33 scaffolding

**Signature :** `sendinstancehubasync(kind, action, requestId, extra)`

**Entrées :**
- `kind` (`InstanceHubKind`) — Dungeon=1 / Raid=2
- `action` (`byte`) — Query=1 / Enter=2 / Leave=3
- `requestId` (`Guid`)
- `extra` — Query/Leave vide ; Enter = `definitionId` (16 octets via `InstanceHubWire.BuildDefinitionIdExtra`)

**Sorties :**
- (`Task`) — paquet `InstanceHubRequest` (90)
- réponses : result **91** / snapshot **92**

**Refus / limites :**
- in-memory only ; pas de `map_instance` PG
- Enter soumis au gate groupe (raid min 2)

**Exemple :**
```csharp
var requestId = Guid.NewGuid();
await client.SendInstanceHubAsync(
    InstanceHubKind.Dungeon, action: 1 /* Query */, requestId, ReadOnlySpan<byte>.Empty);
```

**Voir aussi :** [clientinstancehub](../core/clientinstancehub.md) · [instancehubservice-execute](../server/instancehubservice-execute.md)
