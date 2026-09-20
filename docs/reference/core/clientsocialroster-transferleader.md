# clientsocialroster-transferleader

← [Core](README.md) · [Référence](../README.md)

Transfère le leadership groupe / guilde.

*Source : `ClientSocialRoster.TransferLeader`* · Tip : `d6e59759`

**Signature :** `transferleader(kind, target)`

**Entrées :**
- `kind` (`SocialKind`) — Party / Guild
- `target` (`Guid`) — nouveau chef

**Sorties :**
- (`SocialClientRequest`) — action TransferLeader + Guid payload
