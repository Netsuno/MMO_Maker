# clientsocialroster-accepttarget

← [Core](README.md) · [Référence](../README.md)

Choisit l’id Accept/Decline : ami = CharacterId ; groupe/guilde = SubjectId.

*Source : `ClientSocialRoster.AcceptTarget`* · Tip : `a6edd821`

**Signature :** `accepttarget(item)`

**Entrées :**
- `item` (`SocialListItem`) — ligne liste (invite ou membre)

**Sorties :**
- (`Guid`) — payload Guid pour Accept / Decline

**Exemple :**
```csharp
Guid payload = ClientSocialRoster.AcceptTarget(item);
// Choisit l’id Accept/Decline : ami = CharacterId ; groupe/guilde = SubjectId
// (Guid) — payload Guid pour Accept / Decline
```
