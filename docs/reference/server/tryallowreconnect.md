# tryallowreconnect

← [Server](README.md) · [Référence](../README.md)

Autorise une tentative de reconnexion sous rate-limit.

*Source : `AuthService.TryAllowReconnect`*

**Signature :** `tryallowreconnect(remoteEndPoint)`

**Entrées :**
- `remoteEndPoint` (`string`) — extrémité distante ; seau partagé avec le login
- `username` (`string?`, optionnel) — compte, si déjà connu

**Sorties :**
- (`bool`) — `true` si tentative autorisée

**Exemple :**
```csharp
if (!auth.TryAllowReconnect(remoteEndPoint: "203.0.113.10:443"))
    return; // rate-limit reconnect
// Autorise une tentative de reconnexion sous rate-limit
```
