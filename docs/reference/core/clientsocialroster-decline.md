# clientsocialroster-decline

← [Core](README.md) · [Référence](../README.md)

Construit Decline pour une invitation / demande.

*Source : `ClientSocialRoster.Decline`* · Tip : `a6edd821`

**Signature :** `decline(kind, subjectOrOther)`

**Entrées :**
- `kind` (`SocialKind`) — Party / Guild / Friend
- `subjectOrOther` (`Guid`) — même règle qu’Accept

**Sorties :**
- (`SocialClientRequest`) — action Decline + Guid payload

**Exemple :**
```csharp
var req = ClientSocialRoster.Decline(SocialKind.Friend, subjectOrOther);
await client.SendSocialAsync(req.Kind, req.Action, Guid.NewGuid(), req.Extra);
// Construit Decline pour une invitation / demande
```
