# clientsocialroster-invite

← [Core](README.md) · [Référence](../README.md)

Construit la requête d’invitation / demande d’ami.

*Source : `ClientSocialRoster.Invite`* · Tip : `d6e59759`

**Signature :** `invite(kind, target)`

**Entrées :**
- `kind` (`SocialKind`) — Party / Guild / Friend
- `target` (`Guid`) — personnage cible

**Sorties :**
- (`SocialClientRequest`) — action Invite ou Friend.Request + Guid payload

**Refus :**
- `kind` Block / inconnu → exception
