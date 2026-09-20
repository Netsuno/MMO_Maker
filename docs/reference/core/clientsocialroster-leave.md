# clientsocialroster-leave

← [Core](README.md) · [Référence](../README.md)

Construit Leave groupe ou guilde.

*Source : `ClientSocialRoster.Leave`* · Tip : `d6e59759`

**Signature :** `leave(kind)`

**Entrées :**
- `kind` (`SocialKind`) — Party / Guild

**Sorties :**
- (`SocialClientRequest`) — action Leave, extra vide

**Refus :**
- `kind` Friend → exception
