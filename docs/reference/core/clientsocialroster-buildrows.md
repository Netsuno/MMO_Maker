# clientsocialroster-buildrows

← [Core](README.md) · [Référence](../README.md)

Construit les lignes UI (invites + membres) pour un kind.

*Source : `ClientSocialRoster.BuildRows`* · Tip : `a6edd821`

**Signature :** `buildrows(kind)`

**Entrées :**
- `kind` (`SocialKind`) — Friend / Party / Guild / Block

**Sorties :**
- (`IReadOnlyList<SocialListItem>`) — invites puis membres

**Exemple :**
```csharp
var rows = roster.BuildRows(SocialKind.Friend); // invites puis membres
// Construit les lignes UI (invites + membres) pour un kind
// (IReadOnlyList<SocialListItem>) — invites puis membres
```
