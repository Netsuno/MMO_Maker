# clientsocialroster-emptyhint

← [Core](README.md) · [Référence](../README.md)

Texte d’état vide pour un onglet social (pas de faux membres).

*Source : `ClientSocialRoster.EmptyHint`* · Tip : `a6edd821`

**Signature :** `emptyhint(kind)`

**Entrées :**
- `kind` (`SocialKind`) — Friend / Party / Guild

**Sorties :**
- (`string`) — hint FR, ou vide si des lignes existent

**Exemple :**
```csharp
string hint = roster.EmptyHint(SocialKind.Guild); // "" si lignes présentes
// Texte d’état vide pour un onglet social (pas de faux membres)
// (string) — hint FR, ou vide si des lignes existent
```
