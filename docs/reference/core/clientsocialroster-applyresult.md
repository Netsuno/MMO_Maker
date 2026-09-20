# clientsocialroster-applyresult

← [Core](README.md) · [Référence](../README.md)

Applique le résultat d’une demande sociale (opcode 81).

*Source : `ClientSocialRoster.ApplyResult`* · Tip : `d6e59759`

**Signature :** `applyresult(result)`

**Entrées :**
- `result` (`SocialResultWire`) — succès + message + kind/action/ids

**Sorties :**
- `StatusLine` OK / Refusé
- retire pending si Accept/Decline ami/groupe/guilde réussi
