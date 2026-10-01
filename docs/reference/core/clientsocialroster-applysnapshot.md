# clientsocialroster-applysnapshot

← [Core](README.md) · [Référence](../README.md)

Applique un snapshot social (opcode 82) au roster client.

*Source : `ClientSocialRoster.ApplySnapshot`* · Tip : `a6edd821`

**Signature :** `applysnapshot(snapshot)`

**Entrées :**
- `snapshot` (`SocialSnapshotWire`) — kind + subject + membres + MOTD

**Sorties :**
- met à jour `Party` / `Guild` / `Friends` / `Blocks`
- `StatusLine` mis à jour
- invitations résolues élaguées

**Exemple :**
```csharp
roster.ApplySnapshot(snapshot); // Party / Guild / Friends
// Applique un snapshot social (opcode 82) au roster client
// met à jour Party / Guild / Friends / Blocks
```
