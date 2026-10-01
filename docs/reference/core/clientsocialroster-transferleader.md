# clientsocialroster-transferleader

← [Core](README.md) · [Référence](../README.md)

Transfère le leadership groupe / guilde.

*Source : `ClientSocialRoster.TransferLeader`* · Tip : `a6edd821`

**Signature :** `transferleader(kind, target)`

**Entrées :**
- `kind` (`SocialKind`) — Party / Guild
- `target` (`Guid`) — nouveau chef

**Sorties :**
- (`SocialClientRequest`) — action TransferLeader + Guid payload

**Exemple :**
```csharp
var req = ClientSocialRoster.TransferLeader(SocialKind.Party, target);
await client.SendSocialAsync(req.Kind, req.Action, Guid.NewGuid(), req.Extra);
// Transfère le leadership groupe / guilde
```
