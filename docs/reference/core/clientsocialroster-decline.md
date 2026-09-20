# clientsocialroster-decline

← [Core](README.md) · [Référence](../README.md)

Construit Decline pour une invitation / demande.

*Source : `ClientSocialRoster.Decline`* · Tip : `d6e59759`

**Signature :** `decline(kind, subjectOrOther)`

**Entrées :**
- `kind` (`SocialKind`) — Party / Guild / Friend
- `subjectOrOther` (`Guid`) — même règle qu’Accept

**Sorties :**
- (`SocialClientRequest`) — action Decline + Guid payload
