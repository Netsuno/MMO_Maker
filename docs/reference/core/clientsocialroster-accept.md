# clientsocialroster-accept

← [Core](README.md) · [Référence](../README.md)

Construit Accept (ami = other id ; groupe/guilde = subject id).

*Source : `ClientSocialRoster.Accept`* · Tip : `a6edd821`

**Signature :** `accept(kind, subjectOrOther)`

**Entrées :**
- `kind` (`SocialKind`) — Party / Guild / Friend
- `subjectOrOther` (`Guid`) — voir `accepttarget`

**Sorties :**
- (`SocialClientRequest`) — action Accept + Guid payload

**Exemple :**
```csharp
var req = ClientSocialRoster.Accept(SocialKind.Party, subjectOrOther);
await client.SendSocialAsync(req.Kind, req.Action, Guid.NewGuid(), req.Extra);
// Construit Accept (ami = other id ; groupe/guilde = subject id)
```
