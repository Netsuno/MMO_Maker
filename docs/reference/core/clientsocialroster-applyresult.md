# clientsocialroster-applyresult

← [Core](README.md) · [Référence](../README.md)

Applique le résultat d’une demande sociale (opcode 81).

*Source : `ClientSocialRoster.ApplyResult`* · Tip : `a6edd821`

**Signature :** `applyresult(result)`

**Entrées :**
- `result` (`SocialResultWire`) — succès + message + kind/action/ids

**Sorties :**
- `StatusLine` OK / Refusé
- retire pending si Accept/Decline ami/groupe/guilde réussi

**Exemple :**
```csharp
roster.ApplyResult(result); // StatusLine OK / Refusé
// Applique le résultat d’une demande sociale (opcode 81)
// StatusLine OK / Refusé
```
