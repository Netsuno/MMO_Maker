# clientsocialroster-invite

← [Core](README.md) · [Référence](../README.md)

Construit la requête d’invitation / demande d’ami.

*Source : `ClientSocialRoster.Invite`* · Tip : `a6edd821`

**Signature :** `invite(kind, target)`

**Entrées :**
- `kind` (`SocialKind`) — Party / Guild / Friend
- `target` (`Guid`) — personnage cible

**Sorties :**
- (`SocialClientRequest`) — action Invite ou Friend.Request + Guid payload

**Refus :**
- `kind` Block / inconnu → exception

**Exemple :**
```csharp
var req = ClientSocialRoster.Invite(SocialKind.Party, target);
await client.SendSocialAsync(req.Kind, req.Action, Guid.NewGuid(), req.Extra);
// Construit la requête d’invitation / demande d’ami
```
