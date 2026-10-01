# clientsocialroster-friendremove

← [Core](README.md) · [Référence](../README.md)

Retire un ami (action Friend.Remove).

*Source : `ClientSocialRoster.FriendRemove`* · Tip : `a6edd821`

**Signature :** `friendremove(target)`

**Entrées :**
- `target` (`Guid`) — personnage ami

**Sorties :**
- (`SocialClientRequest`) — Friend.Remove + Guid payload

**Exemple :**
```csharp
var req = ClientSocialRoster.FriendRemove(target);
await client.SendSocialAsync(req.Kind, req.Action, Guid.NewGuid(), req.Extra);
// Retire un ami (action Friend.Remove)
```
