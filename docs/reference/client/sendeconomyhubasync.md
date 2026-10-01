# sendeconomyhubasync

← [Client](README.md) · [Référence](../README.md)

Envoie une enveloppe **EconomyHub** (opcodes 87–89). MVP = **Query seulement**.

*Source : `FrogGameClient.SendEconomyHubAsync`* · Tip : `a6edd821` · #32 scaffolding

**Signature :** `sendeconomyhubasync(kind, action, requestId, extra)`

**Entrées :**
- `kind` (`EconomyHubKind`) — Auction=1 / Mail=2 / GuildBank=3
- `action` (`byte`) — MVP : `Query=1` seulement
- `requestId` (`Guid`) — corrélation
- `extra` (`ReadOnlySpan<byte>`) — Query = vide

**Sorties :**
- (`Task`) — paquet `EconomyHubRequest` (87) envoyé
- réponses attendues : result **88** / snapshot **89**

**Refus / limites :**
- actions mutantes **non implémentées** (serveur renvoie « Action inconnue »)
- pas de gameplay économie (listes vides)

**Exemple :**
```csharp
var requestId = Guid.NewGuid();
await client.SendEconomyHubAsync(
    EconomyHubKind.Auction, action: 1 /* Query */, requestId, extra: ReadOnlySpan<byte>.Empty);
```

**Voir aussi :** [clienteconomyhub](../core/clienteconomyhub.md) · [economyhubservice-executeasync](../server/economyhubservice-executeasync.md)
