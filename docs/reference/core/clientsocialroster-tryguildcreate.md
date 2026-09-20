# clientsocialroster-tryguildcreate

← [Core](README.md) · [Référence](../README.md)

Valide le nom et construit Create guilde.

*Source : `ClientSocialRoster.TryGuildCreate`* · Tip : `d6e59759`

**Signature :** `tryguildcreate(name, out request, out error)`

**Entrées :**
- `name` (`string?`) — nom affiché (`SocialWire.NormalizeGuildName`)

**Sorties :**
- `ok` (`bool`) — true si requête construite
- `request` (`SocialClientRequest`) — Guild.Create + UTF-8
- `error` (`string`) — motif si échec

**Refus :**
- « Nom de guilde requis. »
- « Nom de guilde trop long. » (`SocialProtocolLimits.MaxGuildNameUtf8Bytes`)
