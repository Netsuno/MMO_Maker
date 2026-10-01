# clientsocialroster-leave

← [Core](README.md) · [Référence](../README.md)

Construit Leave groupe ou guilde.

*Source : `ClientSocialRoster.Leave`* · Tip : `a6edd821`

**Signature :** `leave(kind)`

**Entrées :**
- `kind` (`SocialKind`) — Party / Guild

**Sorties :**
- (`SocialClientRequest`) — action Leave, extra vide

**Refus :**
- `kind` Friend → exception

**Exemple :**
```csharp
var req = ClientSocialRoster.Leave(SocialKind.Guild);
await client.SendSocialAsync(req.Kind, req.Action, Guid.NewGuid(), req.Extra);
// Construit Leave groupe ou guilde
```
