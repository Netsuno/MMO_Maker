# clientsocialroster-accept

← [Core](README.md) · [Référence](../README.md)

Construit Accept (ami = other id ; groupe/guilde = subject id).

*Source : `ClientSocialRoster.Accept`* · Tip : `d6e59759`

**Signature :** `accept(kind, subjectOrOther)`

**Entrées :**
- `kind` (`SocialKind`) — Party / Guild / Friend
- `subjectOrOther` (`Guid`) — voir `accepttarget`

**Sorties :**
- (`SocialClientRequest`) — action Accept + Guid payload
