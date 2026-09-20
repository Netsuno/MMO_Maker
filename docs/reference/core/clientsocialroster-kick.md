# clientsocialroster-kick

← [Core](README.md) · [Référence](../README.md)

Construit Kick d’un membre (groupe / guilde).

*Source : `ClientSocialRoster.Kick`* · Tip : `d6e59759`

**Signature :** `kick(kind, target)`

**Entrées :**
- `kind` (`SocialKind`) — Party / Guild
- `target` (`Guid`) — personnage à expulser

**Sorties :**
- (`SocialClientRequest`) — action Kick + Guid payload
