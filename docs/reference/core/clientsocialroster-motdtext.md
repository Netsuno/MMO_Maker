# clientsocialroster-motdtext

← [Core](README.md) · [Référence](../README.md)

Lit le MOTD du snapshot guilde (ou party si présent).

*Source : `ClientSocialRoster.MotdText`* · Tip : `a6edd821`

**Signature :** `motdtext(kind)`

**Entrées :**
- `kind` (`SocialKind`) — Guild / Party

**Sorties :**
- (`string`) — MOTD ou chaîne vide

**Exemple :**
```csharp
string motd = roster.MotdText(SocialKind.Guild);
// Lit le MOTD du snapshot guilde (ou party si présent)
// (string) — MOTD ou chaîne vide
```
