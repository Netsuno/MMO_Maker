# clientsocialroster-disband

← [Core](README.md) · [Référence](../README.md)

Dissout groupe ou guilde (confirm booléen wire).

*Source : `ClientSocialRoster.Disband`* · Tip : `a6edd821`

**Signature :** `disband(kind)`

**Entrées :**
- `kind` (`SocialKind`) — Party / Guild

**Sorties :**
- (`SocialClientRequest`) — action Disband + payload confirm `true`

**Exemple :**
```csharp
var req = ClientSocialRoster.Disband(SocialKind.Party);
await client.SendSocialAsync(req.Kind, req.Action, Guid.NewGuid(), req.Extra);
// Dissout groupe ou guilde (confirm booléen wire)
```
