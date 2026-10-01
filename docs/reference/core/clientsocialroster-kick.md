# clientsocialroster-kick

← [Core](README.md) · [Référence](../README.md)

Construit Kick d’un membre (groupe / guilde).

*Source : `ClientSocialRoster.Kick`* · Tip : `a6edd821`

**Signature :** `kick(kind, target)`

**Entrées :**
- `kind` (`SocialKind`) — Party / Guild
- `target` (`Guid`) — personnage à expulser

**Sorties :**
- (`SocialClientRequest`) — action Kick + Guid payload

**Exemple :**
```csharp
var req = ClientSocialRoster.Kick(SocialKind.Party, target);
await client.SendSocialAsync(req.Kind, req.Action, Guid.NewGuid(), req.Extra);
// Construit Kick d’un membre (groupe / guilde)
```
